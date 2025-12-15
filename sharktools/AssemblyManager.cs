using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SharkTools
{
    /// <summary>
    /// 装配体管理器 - 提供装配体相关功能
    /// 包含添加组件、配合、干涉检查等操作
    /// </summary>
    public class SharkAssemblyManager
    {
        private readonly ISldWorks _swApp;
        private readonly Func<Action, Task> _runOnUIThread;

        public SharkAssemblyManager(ISldWorks swApp, Func<Action, Task> runOnUIThread)
        {
            _swApp = swApp;
            _runOnUIThread = runOnUIThread;
        }

        /// <summary>
        /// 添加组件到装配体
        /// </summary>
        /// <param name="componentPath">组件文件路径</param>
        /// <param name="x">放置X坐标 (米)</param>
        /// <param name="y">放置Y坐标 (米)</param>
        /// <param name="z">放置Z坐标 (米)</param>
        /// <param name="configName">配置名称，可选</param>
        /// <returns>操作结果</returns>
        public async Task<AssemblyResult> AddComponent(string componentPath, double x = 0, double y = 0, double z = 0, string configName = "")
        {
            var result = new AssemblyResult();

            await _runOnUIThread(() =>
            {
                try
                {
                    var model = _swApp.ActiveDoc as ModelDoc2;
                    if (model == null)
                    {
                        result.Success = false;
                        result.Message = "没有打开的文档";
                        return;
                    }

                    if (model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
                    {
                        result.Success = false;
                        result.Message = "当前文档不是装配体";
                        return;
                    }

                    if (!File.Exists(componentPath))
                    {
                        result.Success = false;
                        result.Message = $"组件文件不存在: {componentPath}";
                        return;
                    }

                    var assemblyDoc = model as AssemblyDoc;
                    
                    // 添加组件
                    var component = assemblyDoc.AddComponent5(
                        componentPath,
                        (int)swAddComponentConfigOptions_e.swAddComponentConfigOptions_CurrentSelectedConfig,
                        configName,
                        false,          // UseConfigForMateSetting
                        "",             // NewPartTemplateFileName
                        x, y, z         // 位置坐标
                    ) as Component2;

                    if (component != null)
                    {
                        result.Success = true;
                        result.Message = "组件添加成功";
                        result.ComponentName = component.Name2;
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = "组件添加失败";
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"添加组件失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 添加重合配合
        /// </summary>
        /// <param name="align">是否对齐</param>
        /// <returns>操作结果</returns>
        public async Task<AssemblyResult> AddCoincidentMate(bool align = true)
        {
            var result = new AssemblyResult();

            await _runOnUIThread(() =>
            {
                try
                {
                    var model = _swApp.ActiveDoc as ModelDoc2;
                    if (model == null || model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
                    {
                        result.Success = false;
                        result.Message = "请在装配体中操作";
                        return;
                    }

                    var assemblyDoc = model as AssemblyDoc;
                    
                    // 检查选择
                    var selMgr = model.SelectionManager as SelectionMgr;
                    if (selMgr.GetSelectedObjectCount2(-1) < 2)
                    {
                        result.Success = false;
                        result.Message = "请选择两个要配合的实体";
                        return;
                    }

                    // 添加重合配合
                    int errors = 0;
                    var mate = assemblyDoc.AddMate5(
                        (int)swMateType_e.swMateCOINCIDENT,
                        align ? (int)swMateAlign_e.swMateAlignALIGNED : (int)swMateAlign_e.swMateAlignANTI_ALIGNED,
                        false,          // flip
                        0, 0, 0, 0, 0, 0, 0, 0, // distance, angle, etc.
                        false,          // forPositioningOnly
                        false,          // lockRotation
                        0,              // widthMateOption
                        out errors
                    ) as Mate2;

                    if (mate != null)
                    {
                        result.Success = true;
                        result.Message = "重合配合添加成功";
                        result.MateName = "CoincidentMate";
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = $"重合配合添加失败 (错误码: {errors})";
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"添加配合失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 添加同心配合
        /// </summary>
        /// <returns>操作结果</returns>
        public async Task<AssemblyResult> AddConcentricMate()
        {
            var result = new AssemblyResult();

            await _runOnUIThread(() =>
            {
                try
                {
                    var model = _swApp.ActiveDoc as ModelDoc2;
                    if (model == null || model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
                    {
                        result.Success = false;
                        result.Message = "请在装配体中操作";
                        return;
                    }

                    var assemblyDoc = model as AssemblyDoc;
                    
                    var selMgr = model.SelectionManager as SelectionMgr;
                    if (selMgr.GetSelectedObjectCount2(-1) < 2)
                    {
                        result.Success = false;
                        result.Message = "请选择两个圆柱面或圆边";
                        return;
                    }

                    int errors = 0;
                    var mate = assemblyDoc.AddMate5(
                        (int)swMateType_e.swMateCONCENTRIC,
                        (int)swMateAlign_e.swMateAlignALIGNED,
                        false,
                        0, 0, 0, 0, 0, 0, 0, 0,
                        false, false, 0,
                        out errors
                    ) as Mate2;

                    if (mate != null)
                    {
                        result.Success = true;
                        result.Message = "同心配合添加成功";
                        result.MateName = "ConcentricMate";
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = $"同心配合添加失败 (错误码: {errors})";
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"添加配合失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 添加距离配合
        /// </summary>
        /// <param name="distance">距离值 (米)</param>
        /// <returns>操作结果</returns>
        public async Task<AssemblyResult> AddDistanceMate(double distance)
        {
            var result = new AssemblyResult();

            await _runOnUIThread(() =>
            {
                try
                {
                    var model = _swApp.ActiveDoc as ModelDoc2;
                    if (model == null || model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
                    {
                        result.Success = false;
                        result.Message = "请在装配体中操作";
                        return;
                    }

                    var assemblyDoc = model as AssemblyDoc;
                    
                    var selMgr = model.SelectionManager as SelectionMgr;
                    if (selMgr.GetSelectedObjectCount2(-1) < 2)
                    {
                        result.Success = false;
                        result.Message = "请选择两个要配合的实体";
                        return;
                    }

                    int errors = 0;
                    var mate = assemblyDoc.AddMate5(
                        (int)swMateType_e.swMateDISTANCE,
                        (int)swMateAlign_e.swMateAlignALIGNED,
                        false,
                        distance,       // Distance
                        0, 0, 0, 0, 0, 0, 0,
                        false, false, 0,
                        out errors
                    ) as Mate2;

                    if (mate != null)
                    {
                        result.Success = true;
                        result.Message = $"距离配合添加成功 (距离: {distance * 1000}mm)";
                        result.MateName = "DistanceMate";
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = $"距离配合添加失败 (错误码: {errors})";
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"添加配合失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 添加角度配合
        /// </summary>
        /// <param name="angle">角度值 (度)</param>
        /// <returns>操作结果</returns>
        public async Task<AssemblyResult> AddAngleMate(double angle)
        {
            var result = new AssemblyResult();

            await _runOnUIThread(() =>
            {
                try
                {
                    var model = _swApp.ActiveDoc as ModelDoc2;
                    if (model == null || model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
                    {
                        result.Success = false;
                        result.Message = "请在装配体中操作";
                        return;
                    }

                    var assemblyDoc = model as AssemblyDoc;
                    double angleRad = angle * Math.PI / 180.0;

                    int errors = 0;
                    var mate = assemblyDoc.AddMate5(
                        (int)swMateType_e.swMateANGLE,
                        (int)swMateAlign_e.swMateAlignALIGNED,
                        false,
                        0,              // Distance
                        0,              // Distance tolerance
                        0,              // Distance tolerance min
                        angleRad,       // Angle
                        0, 0, 0, 0,
                        false, false, 0,
                        out errors
                    ) as Mate2;

                    if (mate != null)
                    {
                        result.Success = true;
                        result.Message = $"角度配合添加成功 (角度: {angle}°)";
                        result.MateName = "AngleMate";
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = $"角度配合添加失败 (错误码: {errors})";
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"添加配合失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 添加平行配合
        /// </summary>
        /// <returns>操作结果</returns>
        public async Task<AssemblyResult> AddParallelMate()
        {
            var result = new AssemblyResult();

            await _runOnUIThread(() =>
            {
                try
                {
                    var model = _swApp.ActiveDoc as ModelDoc2;
                    if (model == null || model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
                    {
                        result.Success = false;
                        result.Message = "请在装配体中操作";
                        return;
                    }

                    var assemblyDoc = model as AssemblyDoc;

                    int errors = 0;
                    var mate = assemblyDoc.AddMate5(
                        (int)swMateType_e.swMatePARALLEL,
                        (int)swMateAlign_e.swMateAlignALIGNED,
                        false,
                        0, 0, 0, 0, 0, 0, 0, 0,
                        false, false, 0,
                        out errors
                    ) as Mate2;

                    if (mate != null)
                    {
                        result.Success = true;
                        result.Message = "平行配合添加成功";
                        result.MateName = "ParallelMate";
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = $"平行配合添加失败 (错误码: {errors})";
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"添加配合失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 添加垂直配合
        /// </summary>
        /// <returns>操作结果</returns>
        public async Task<AssemblyResult> AddPerpendicularMate()
        {
            var result = new AssemblyResult();

            await _runOnUIThread(() =>
            {
                try
                {
                    var model = _swApp.ActiveDoc as ModelDoc2;
                    if (model == null || model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
                    {
                        result.Success = false;
                        result.Message = "请在装配体中操作";
                        return;
                    }

                    var assemblyDoc = model as AssemblyDoc;

                    int errors = 0;
                    var mate = assemblyDoc.AddMate5(
                        (int)swMateType_e.swMatePERPENDICULAR,
                        (int)swMateAlign_e.swMateAlignALIGNED,
                        false,
                        0, 0, 0, 0, 0, 0, 0, 0,
                        false, false, 0,
                        out errors
                    ) as Mate2;

                    if (mate != null)
                    {
                        result.Success = true;
                        result.Message = "垂直配合添加成功";
                        result.MateName = "PerpendicularMate";
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = $"垂直配合添加失败 (错误码: {errors})";
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"添加配合失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 添加相切配合
        /// </summary>
        /// <returns>操作结果</returns>
        public async Task<AssemblyResult> AddTangentMate()
        {
            var result = new AssemblyResult();

            await _runOnUIThread(() =>
            {
                try
                {
                    var model = _swApp.ActiveDoc as ModelDoc2;
                    if (model == null || model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
                    {
                        result.Success = false;
                        result.Message = "请在装配体中操作";
                        return;
                    }

                    var assemblyDoc = model as AssemblyDoc;

                    int errors = 0;
                    var mate = assemblyDoc.AddMate5(
                        (int)swMateType_e.swMateTANGENT,
                        (int)swMateAlign_e.swMateAlignALIGNED,
                        false,
                        0, 0, 0, 0, 0, 0, 0, 0,
                        false, false, 0,
                        out errors
                    ) as Mate2;

                    if (mate != null)
                    {
                        result.Success = true;
                        result.Message = "相切配合添加成功";
                        result.MateName = "TangentMate";
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = $"相切配合添加失败 (错误码: {errors})";
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"添加配合失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 固定组件
        /// </summary>
        /// <param name="componentName">组件名称</param>
        /// <returns>操作结果</returns>
        public async Task<AssemblyResult> FixComponent(string componentName)
        {
            var result = new AssemblyResult();

            await _runOnUIThread(() =>
            {
                try
                {
                    var model = _swApp.ActiveDoc as ModelDoc2;
                    if (model == null || model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
                    {
                        result.Success = false;
                        result.Message = "请在装配体中操作";
                        return;
                    }

                    // 选择组件
                    bool selected = model.Extension.SelectByID2(componentName, "COMPONENT", 0, 0, 0, false, 0, null, 0);
                    if (!selected)
                    {
                        result.Success = false;
                        result.Message = $"无法选择组件: {componentName}";
                        return;
                    }

                    var assemblyDoc = model as AssemblyDoc;
                    assemblyDoc.FixComponent();

                    result.Success = true;
                    result.Message = $"组件 {componentName} 已固定";
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"固定组件失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 浮动组件（取消固定）
        /// </summary>
        /// <param name="componentName">组件名称</param>
        /// <returns>操作结果</returns>
        public async Task<AssemblyResult> FloatComponent(string componentName)
        {
            var result = new AssemblyResult();

            await _runOnUIThread(() =>
            {
                try
                {
                    var model = _swApp.ActiveDoc as ModelDoc2;
                    if (model == null || model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
                    {
                        result.Success = false;
                        result.Message = "请在装配体中操作";
                        return;
                    }

                    bool selected = model.Extension.SelectByID2(componentName, "COMPONENT", 0, 0, 0, false, 0, null, 0);
                    if (!selected)
                    {
                        result.Success = false;
                        result.Message = $"无法选择组件: {componentName}";
                        return;
                    }

                    var assemblyDoc = model as AssemblyDoc;
                    assemblyDoc.UnfixComponent();

                    result.Success = true;
                    result.Message = $"组件 {componentName} 已浮动";
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"浮动组件失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 隐藏组件
        /// </summary>
        /// <param name="componentName">组件名称</param>
        /// <returns>操作结果</returns>
        public async Task<AssemblyResult> HideComponent(string componentName)
        {
            var result = new AssemblyResult();

            await _runOnUIThread(() =>
            {
                try
                {
                    var model = _swApp.ActiveDoc as ModelDoc2;
                    if (model == null || model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
                    {
                        result.Success = false;
                        result.Message = "请在装配体中操作";
                        return;
                    }

                    bool selected = model.Extension.SelectByID2(componentName, "COMPONENT", 0, 0, 0, false, 0, null, 0);
                    if (!selected)
                    {
                        result.Success = false;
                        result.Message = $"无法选择组件: {componentName}";
                        return;
                    }

                    var assemblyDoc = model as AssemblyDoc;
                    assemblyDoc.HideComponent();

                    result.Success = true;
                    result.Message = $"组件 {componentName} 已隐藏";
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"隐藏组件失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 显示组件
        /// </summary>
        /// <param name="componentName">组件名称</param>
        /// <returns>操作结果</returns>
        public async Task<AssemblyResult> ShowComponent(string componentName)
        {
            var result = new AssemblyResult();

            await _runOnUIThread(() =>
            {
                try
                {
                    var model = _swApp.ActiveDoc as ModelDoc2;
                    if (model == null || model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
                    {
                        result.Success = false;
                        result.Message = "请在装配体中操作";
                        return;
                    }

                    bool selected = model.Extension.SelectByID2(componentName, "COMPONENT", 0, 0, 0, false, 0, null, 0);
                    if (!selected)
                    {
                        result.Success = false;
                        result.Message = $"无法选择组件: {componentName}";
                        return;
                    }

                    var assemblyDoc = model as AssemblyDoc;
                    assemblyDoc.ShowComponent();

                    result.Success = true;
                    result.Message = $"组件 {componentName} 已显示";
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"显示组件失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 获取装配体组件列表
        /// </summary>
        /// <returns>组件列表结果</returns>
        public async Task<ComponentListResult> GetComponentList()
        {
            var result = new ComponentListResult();

            await _runOnUIThread(() =>
            {
                try
                {
                    var model = _swApp.ActiveDoc as ModelDoc2;
                    if (model == null || model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
                    {
                        result.Success = false;
                        result.Message = "请在装配体中操作";
                        return;
                    }

                    var assemblyDoc = model as AssemblyDoc;
                    var components = assemblyDoc.GetComponents(true) as object[];
                    
                    result.Components = new List<ComponentInfo>();

                    if (components != null)
                    {
                        foreach (var comp in components)
                        {
                            var component = comp as Component2;
                            if (component != null)
                            {
                                result.Components.Add(new ComponentInfo
                                {
                                    Name = component.Name2,
                                    PathName = component.GetPathName(),
                                    IsFixed = component.IsFixed(),
                                    IsHidden = component.Visible == (int)swComponentVisibilityState_e.swComponentHidden,
                                    IsSuppressed = component.IsSuppressed()
                                });
                            }
                        }
                    }

                    result.Success = true;
                    result.Message = $"找到 {result.Components.Count} 个组件";
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"获取组件列表失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 检测干涉
        /// </summary>
        /// <returns>干涉检测结果</returns>
        public async Task<InterferenceResult> DetectInterference()
        {
            var result = new InterferenceResult();

            await _runOnUIThread(() =>
            {
                try
                {
                    var model = _swApp.ActiveDoc as ModelDoc2;
                    if (model == null || model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
                    {
                        result.Success = false;
                        result.Message = "请在装配体中操作";
                        return;
                    }

                    var assemblyDoc = model as AssemblyDoc;
                    var interferenceMgr = assemblyDoc.InterferenceDetectionManager;
                    
                    // 获取干涉检测结果
                    var interferences = interferenceMgr.GetInterferences() as object[];
                    int interferenceCount = interferences?.Length ?? 0;
                    
                    result.Interferences = new List<InterferenceInfo>();
                    
                    if (interferenceCount > 0 && interferences != null)
                    {
                        for (int i = 0; i < interferenceCount; i++)
                        {
                            var interference = interferences[i] as IInterference;
                            if (interference != null)
                            {
                                var components = interference.Components as object[];
                                var comp1 = components?[0] as Component2;
                                var comp2 = components?[1] as Component2;
                                
                                result.Interferences.Add(new InterferenceInfo
                                {
                                    Component1 = comp1?.Name2 ?? "Unknown",
                                    Component2 = comp2?.Name2 ?? "Unknown",
                                    Volume = interference.Volume
                                });
                            }
                        }
                    }

                    result.Success = true;
                    result.HasInterference = result.Interferences.Count > 0;
                    result.Message = result.HasInterference 
                        ? $"检测到 {result.Interferences.Count} 处干涉" 
                        : "未检测到干涉";
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"干涉检测失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 选择组件面
        /// </summary>
        /// <param name="componentName">组件名称</param>
        /// <param name="faceName">面名称</param>
        /// <param name="append">是否追加选择</param>
        /// <returns>操作结果</returns>
        public async Task<AssemblyResult> SelectComponentFace(string componentName, string faceName, bool append = false)
        {
            var result = new AssemblyResult();

            await _runOnUIThread(() =>
            {
                try
                {
                    var model = _swApp.ActiveDoc as ModelDoc2;
                    if (model == null || model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
                    {
                        result.Success = false;
                        result.Message = "请在装配体中操作";
                        return;
                    }

                    // 构建完整的选择名称
                    string selName = $"{faceName}@{componentName}";
                    bool selected = model.Extension.SelectByID2(selName, "FACE", 0, 0, 0, append, 0, null, 0);
                    
                    if (selected)
                    {
                        result.Success = true;
                        result.Message = $"面 {selName} 选择成功";
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = $"无法选择面: {selName}";
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"选择面失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 选择组件边线
        /// </summary>
        /// <param name="componentName">组件名称</param>
        /// <param name="edgeName">边线名称</param>
        /// <param name="append">是否追加选择</param>
        /// <returns>操作结果</returns>
        public async Task<AssemblyResult> SelectComponentEdge(string componentName, string edgeName, bool append = false)
        {
            var result = new AssemblyResult();

            await _runOnUIThread(() =>
            {
                try
                {
                    var model = _swApp.ActiveDoc as ModelDoc2;
                    if (model == null || model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
                    {
                        result.Success = false;
                        result.Message = "请在装配体中操作";
                        return;
                    }

                    string selName = $"{edgeName}@{componentName}";
                    bool selected = model.Extension.SelectByID2(selName, "EDGE", 0, 0, 0, append, 0, null, 0);
                    
                    if (selected)
                    {
                        result.Success = true;
                        result.Message = $"边线 {selName} 选择成功";
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = $"无法选择边线: {selName}";
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"选择边线失败: {ex.Message}";
                }
            });

            return result;
        }
    }

    /// <summary>
    /// 装配体操作结果
    /// </summary>
    public class AssemblyResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string ComponentName { get; set; }
        public string MateName { get; set; }
    }

    /// <summary>
    /// 组件信息
    /// </summary>
    public class ComponentInfo
    {
        public string Name { get; set; }
        public string PathName { get; set; }
        public bool IsFixed { get; set; }
        public bool IsHidden { get; set; }
        public bool IsSuppressed { get; set; }
    }

    /// <summary>
    /// 组件列表结果
    /// </summary>
    public class ComponentListResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<ComponentInfo> Components { get; set; }
    }

    /// <summary>
    /// 干涉信息
    /// </summary>
    public class InterferenceInfo
    {
        public string Component1 { get; set; }
        public string Component2 { get; set; }
        public double Volume { get; set; }
    }

    /// <summary>
    /// 干涉检测结果
    /// </summary>
    public class InterferenceResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public bool HasInterference { get; set; }
        public List<InterferenceInfo> Interferences { get; set; }
    }
}
