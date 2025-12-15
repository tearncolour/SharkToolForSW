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
    /// 特征管理器 - 提供特征创建相关功能
    /// 包含拉伸、切除、扫描、放样、旋转等特征操作
    /// </summary>
    public class SharkFeatureCreator
    {
        private readonly ISldWorks _swApp;
        private readonly Func<Action, Task> _runOnUIThread;

        public SharkFeatureCreator(ISldWorks swApp, Func<Action, Task> runOnUIThread)
        {
            _swApp = swApp;
            _runOnUIThread = runOnUIThread;
        }

        /// <summary>
        /// 拉伸凸台/基体特征
        /// </summary>
        /// <param name="depth">拉伸深度 (米)</param>
        /// <param name="direction">拉伸方向: true=正向, false=反向</param>
        /// <param name="draftAngle">拔模角度 (度)，可选</param>
        /// <param name="draftOutward">拔模向外: true=向外, false=向内</param>
        /// <returns>操作结果</returns>
        public async Task<FeatureResult> ExtrudeBoss(double depth, bool direction = true, double draftAngle = 0, bool draftOutward = false)
        {
            var result = new FeatureResult();

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

                    var featMgr = model.FeatureManager;
                    
                    // 将角度转换为弧度
                    double draftRad = draftAngle * Math.PI / 180.0;
                    
                    // 创建拉伸特征
                    var feature = featMgr.FeatureExtrusion3(
                        true,           // Sd - 单向
                        false,          // Flip - 不翻转
                        direction,      // Dir - 方向
                        (int)swEndConditions_e.swEndCondBlind, // T1 - 盲孔终止
                        (int)swEndConditions_e.swEndCondBlind, // T2 - 盲孔终止
                        depth,          // D1 - 深度1
                        0,              // D2 - 深度2
                        draftAngle != 0,// Dchk1 - 是否拔模1
                        false,          // Dchk2 - 是否拔模2
                        draftOutward,   // Ddir1 - 拔模方向1
                        false,          // Ddir2 - 拔模方向2
                        draftRad,       // Dang1 - 拔模角度1
                        0,              // Dang2 - 拔模角度2
                        false,          // OffsetReverse1
                        false,          // OffsetReverse2
                        false,          // TranslateSurface1
                        false,          // TranslateSurface2
                        true,           // Merge - 合并结果
                        true,           // UseFeatScope
                        false,          // UseAutoSelect
                        (int)swStartConditions_e.swStartSketchPlane, // StartCondition
                        0,              // StartOffset
                        false           // FlipStartOffset
                    ) as Feature;

                    if (feature != null)
                    {
                        result.Success = true;
                        result.Message = "拉伸凸台创建成功";
                        result.FeatureName = feature.Name;
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = "拉伸凸台创建失败，请确保已选择封闭的草图轮廓";
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"拉伸凸台创建失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 拉伸切除特征
        /// </summary>
        /// <param name="depth">切除深度 (米)</param>
        /// <param name="direction">切除方向: true=正向, false=反向</param>
        /// <param name="throughAll">是否完全贯穿</param>
        /// <param name="draftAngle">拔模角度 (度)，可选</param>
        /// <returns>操作结果</returns>
        public async Task<FeatureResult> ExtrudeCut(double depth, bool direction = true, bool throughAll = false, double draftAngle = 0)
        {
            var result = new FeatureResult();

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

                    var featMgr = model.FeatureManager;
                    double draftRad = draftAngle * Math.PI / 180.0;
                    
                    int endCondition = throughAll ? 
                        (int)swEndConditions_e.swEndCondThroughAll : 
                        (int)swEndConditions_e.swEndCondBlind;

                    // 创建切除特征
                    var feature = featMgr.FeatureCut4(
                        true,           // Sd - 单向
                        false,          // Flip
                        direction,      // Dir
                        endCondition,   // T1
                        0,              // T2
                        depth,          // D1
                        0,              // D2
                        draftAngle != 0,// Dchk1
                        false,          // Dchk2
                        false,          // Ddir1
                        false,          // Ddir2
                        draftRad,       // Dang1
                        0,              // Dang2
                        false,          // OffsetReverse1
                        false,          // OffsetReverse2
                        false,          // TranslateSurface1
                        false,          // TranslateSurface2
                        false,          // NormalCut
                        false,          // UseFeatScope
                        false,          // UseAutoSelect
                        false,          // AssemblyFeatureScope
                        false,          // AutoSelectComponents
                        false,          // PropagateFeatureToParts
                        (int)swStartConditions_e.swStartSketchPlane,
                        0,              // StartOffset
                        false,          // FlipStartOffset
                        false           // OptimizeGeometry
                    ) as Feature;

                    if (feature != null)
                    {
                        result.Success = true;
                        result.Message = "拉伸切除创建成功";
                        result.FeatureName = feature.Name;
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = "拉伸切除创建失败，请确保已选择封闭的草图轮廓";
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"拉伸切除创建失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 旋转凸台/基体特征
        /// </summary>
        /// <param name="angle">旋转角度 (度)，360为完整旋转</param>
        /// <param name="direction">旋转方向: true=正向, false=反向</param>
        /// <returns>操作结果</returns>
        public async Task<FeatureResult> RevolveBoss(double angle = 360, bool direction = true)
        {
            var result = new FeatureResult();

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

                    var featMgr = model.FeatureManager;
                    double angleRad = angle * Math.PI / 180.0;
                    
                    // 创建旋转特征
                    var feature = featMgr.FeatureRevolve2(
                        true,           // SingleDir
                        true,           // IsSolid
                        false,          // IsThin
                        false,          // IsCut
                        direction,      // ReverseDir
                        false,          // BothDirectionUpToSameEntity
                        (int)swEndConditions_e.swEndCondBlind, // Dir1Type
                        (int)swEndConditions_e.swEndCondBlind, // Dir2Type
                        angleRad,       // Dir1Angle
                        0,              // Dir2Angle
                        false,          // OffsetReverse1
                        false,          // OffsetReverse2
                        0,              // OffsetDistance1
                        0,              // OffsetDistance2
                        (int)swThinWallType_e.swThinWallMidPlane,  // ThinType
                        0,              // ThinThickness1
                        0,              // ThinThickness2
                        true,           // Merge
                        true,           // UseFeatScope
                        false           // UseAutoSelect
                    ) as Feature;

                    if (feature != null)
                    {
                        result.Success = true;
                        result.Message = "旋转凸台创建成功";
                        result.FeatureName = feature.Name;
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = "旋转凸台创建失败，请确保已选择草图轮廓和旋转轴";
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"旋转凸台创建失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 旋转切除特征
        /// </summary>
        /// <param name="angle">旋转角度 (度)</param>
        /// <param name="direction">旋转方向</param>
        /// <returns>操作结果</returns>
        public async Task<FeatureResult> RevolveCut(double angle = 360, bool direction = true)
        {
            var result = new FeatureResult();

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

                    var featMgr = model.FeatureManager;
                    double angleRad = angle * Math.PI / 180.0;
                    
                    // 使用 FeatureRevolve2 并设置 IsCut = true
                    var feature = featMgr.FeatureRevolve2(
                        true,           // SingleDir
                        true,           // IsSolid
                        false,          // IsThin
                        true,           // IsCut - 切除
                        direction,      // ReverseDir
                        false,          // BothDirectionUpToSameEntity
                        (int)swEndConditions_e.swEndCondBlind, // Dir1Type
                        (int)swEndConditions_e.swEndCondBlind, // Dir2Type
                        angleRad,       // Dir1Angle
                        0,              // Dir2Angle
                        false,          // OffsetReverse1
                        false,          // OffsetReverse2
                        0,              // OffsetDistance1
                        0,              // OffsetDistance2
                        (int)swThinWallType_e.swThinWallMidPlane,  // ThinType
                        0,              // ThinThickness1
                        0,              // ThinThickness2
                        true,           // Merge
                        true,           // UseFeatScope
                        false           // UseAutoSelect
                    ) as Feature;

                    if (feature != null)
                    {
                        result.Success = true;
                        result.Message = "旋转切除创建成功";
                        result.FeatureName = feature.Name;
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = "旋转切除创建失败";
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"旋转切除创建失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 扫描凸台特征
        /// </summary>
        /// <param name="thinWall">是否薄壁特征</param>
        /// <param name="thickness">薄壁厚度 (米)</param>
        /// <returns>操作结果</returns>
        public async Task<FeatureResult> SweepBoss(bool thinWall = false, double thickness = 0)
        {
            var result = new FeatureResult();

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

                    var featMgr = model.FeatureManager;
                    
                    // InsertProtrusionSwept4 - 20个参数
                    // 使用前需要先选择: Mark 1 = 轮廓草图, Mark 2 = 引导曲线(可选), Mark 4 = 路径
                    var feature = featMgr.InsertProtrusionSwept4(
                        false,          // Propagate - 是否传播到下一个切边
                        false,          // Alignment - 对齐方式
                        (int)swTwistControlType_e.swTwistControlFollowPath, // TwistCtrlOption - 扭转控制
                        true,           // KeepTangency - 保持相切
                        false,          // BAdvancedSmoothing - 高级平滑
                        (int)swTangencyType_e.swTangencyNone, // StartMatchingType - 起始切线类型
                        (int)swTangencyType_e.swTangencyNone, // EndMatchingType - 终止切线类型
                        thinWall,       // IsThinBody - 是否薄壁
                        thickness,      // Thickness1 - 厚度1
                        thickness,      // Thickness2 - 厚度2
                        thinWall ? (int)swThinWallType_e.swThinWallMidPlane : 0, // ThinType - 薄壁类型
                        0,              // PathAlign - 路径对齐
                        true,           // Merge - 合并结果
                        true,           // UseFeatScope - 使用特征范围
                        false,          // UseAutoSelect - 自动选择
                        0,              // TwistAngle - 扭转角度
                        false,          // BMergeSmoothFaces - 合并平滑面
                        false,          // CircularProfile - 圆形轮廓
                        0,              // CircularProfileDiameter - 圆形轮廓直径
                        (int)swSweepDirection_e.swSweepBidirectional // Direction - 方向
                    ) as Feature;

                    if (feature != null)
                    {
                        result.Success = true;
                        result.Message = "扫描凸台创建成功";
                        result.FeatureName = feature.Name;
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = "扫描凸台创建失败，请确保已正确选择轮廓草图(Mark=1)和路径(Mark=4)";
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"扫描凸台创建失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 扫描切除特征
        /// </summary>
        /// <returns>操作结果</returns>
        public async Task<FeatureResult> SweepCut()
        {
            var result = new FeatureResult();

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

                    var featMgr = model.FeatureManager;
                    
                    // InsertCutSwept5 - 22个参数
                    // 使用前需要先选择: Mark 1 = 轮廓草图或工具体, Mark 2 = 引导曲线(可选), Mark 4 = 路径
                    var feature = featMgr.InsertCutSwept5(
                        false,          // Propagate - 是否传播到下一个边
                        false,          // Alignment - 对齐方式
                        (int)swTwistControlType_e.swTwistControlFollowPath, // TwistCtrlOption - 扭转控制
                        true,           // KeepTangency - 保持相切
                        false,          // BAdvancedSmoothing - 高级平滑
                        (int)swTangencyType_e.swTangencyNone, // StartMatchingType - 起始切线类型
                        (int)swTangencyType_e.swTangencyNone, // EndMatchingType - 终止切线类型
                        false,          // IsThinBody - 是否薄壁
                        0,              // Thickness1 - 厚度1
                        0,              // Thickness2 - 厚度2
                        0,              // ThinType - 薄壁类型
                        0,              // PathAlign - 路径对齐
                        true,           // UseFeatScope - 使用特征范围
                        false,          // UseAutoSelect - 自动选择
                        0,              // TwistAngle - 扭转角度
                        false,          // BMergeSmoothFaces - 合并平滑面
                        false,          // AssemblyFeatureScope - 装配体特征范围
                        false,          // AutoSelectComponents - 自动选择零部件
                        false,          // PropagateFeatureToParts - 传播特征到零件
                        false,          // CircularProfile - 圆形轮廓
                        0,              // CircularProfileDiameter - 圆形轮廓直径
                        (int)swSweepDirection_e.swSweepBidirectional // Direction - 方向
                    ) as Feature;

                    if (feature != null)
                    {
                        result.Success = true;
                        result.Message = "扫描切除创建成功";
                        result.FeatureName = feature.Name;
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = "扫描切除创建失败，请确保已正确选择轮廓草图(Mark=1)和路径(Mark=4)";
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"扫描切除创建失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 放样凸台特征
        /// </summary>
        /// <returns>操作结果</returns>
        public async Task<FeatureResult> LoftBoss()
        {
            var result = new FeatureResult();

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

                    var featMgr = model.FeatureManager;
                    
                    var feature = featMgr.InsertProtrusionBlend(
                        false,          // Closed
                        true,           // KeepTangency
                        false,          // ForceNonRational
                        1.0,            // TessToleranceFactor
                        0,              // StartMatchingType
                        0,              // EndMatchingType
                        0.0, 0.0,       // StartTangentLength, EndTangentLength
                        false,          // StartTangentDir
                        false,          // EndTangentDir
                        false,          // IsThinBody
                        0, 0,           // Thickness1, Thickness2
                        0,              // ThinType
                        true,           // Merge
                        true,           // UseFeatScope
                        false           // UseAutoSelect
                    ) as Feature;

                    if (feature != null)
                    {
                        result.Success = true;
                        result.Message = "放样凸台创建成功";
                        result.FeatureName = feature.Name;
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = "放样凸台创建失败，请确保已选择多个轮廓草图";
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"放样凸台创建失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 放样切除特征
        /// </summary>
        /// <returns>操作结果</returns>
        public async Task<FeatureResult> LoftCut()
        {
            var result = new FeatureResult();

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

                    var featMgr = model.FeatureManager;
                    
                    // InsertCutBlend - 12个参数
                    // 使用前需要先选择: Mark 1 = 轮廓, Mark 2 = 引导曲线(可选), Mark 4 = 中心线(可选)
                    var feature = featMgr.InsertCutBlend(
                        false,          // Closed - 是否闭合
                        true,           // KeepTangency - 保持相切
                        false,          // ForceNonRational - 强制非有理
                        1.0,            // TessToleranceFactor - 细分公差因子
                        0,              // StartMatchingType - 起始匹配类型 (0=none)
                        0,              // EndMatchingType - 终止匹配类型 (0=none)
                        false,          // IsThinBody - 是否薄壁
                        0,              // Thickness1 - 厚度1
                        0,              // Thickness2 - 厚度2
                        0,              // ThinType - 薄壁类型
                        true,           // UseFeatScope - 使用特征范围
                        false           // UseAutoSelect - 自动选择
                    ) as Feature;

                    if (feature != null)
                    {
                        result.Success = true;
                        result.Message = "放样切除创建成功";
                        result.FeatureName = feature.Name;
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = "放样切除创建失败，请确保已按顺序选择多个轮廓草图(Mark=1)";
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"放样切除创建失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 圆角特征
        /// </summary>
        /// <param name="radius">圆角半径 (米)</param>
        /// <returns>操作结果</returns>
        public async Task<FeatureResult> Fillet(double radius)
        {
            var result = new FeatureResult();

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

                    var featMgr = model.FeatureManager;
                    
                    // 创建简单圆角 - FeatureFillet3 需要14个参数
                    var feature = featMgr.FeatureFillet3(
                        (int)swFeatureFilletOptions_e.swFeatureFilletUniformRadius,  // Options
                        radius,         // R1 - 半径
                        0,              // R2
                        0,              // Rho
                        (int)swFeatureFilletType_e.swFeatureFilletType_Simple,  // Ftyp
                        (int)swFilletOverFlowType_e.swFilletOverFlowType_Default,  // OverflowType
                        (int)swFeatureFilletProfileType_e.swFeatureFilletCircular,  // ConicRhoType
                        null,           // Radii
                        null,           // Dist2Arr
                        null,           // RhoArr
                        null,           // SetBackDistances
                        null,           // PointRadiusArray
                        null,           // PointDist2Array
                        null            // PointRhoArray
                    ) as Feature;

                    if (feature != null)
                    {
                        result.Success = true;
                        result.Message = "圆角创建成功";
                        result.FeatureName = feature.Name;
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = "圆角创建失败，请先选择边线";
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"圆角创建失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 倒角特征
        /// </summary>
        /// <param name="distance">倒角距离 (米)</param>
        /// <param name="angle">倒角角度 (度)，默认45度</param>
        /// <returns>操作结果</returns>
        public async Task<FeatureResult> Chamfer(double distance, double angle = 45)
        {
            var result = new FeatureResult();

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

                    var featMgr = model.FeatureManager;
                    double angleRad = angle * Math.PI / 180.0;
                    
                    // 创建倒角
                    var feature = featMgr.InsertFeatureChamfer(
                        4,              // Options - 距离-角度倒角
                        (int)swChamferType_e.swChamferAngleDistance,
                        distance,       // Width
                        angleRad,       // Angle
                        0,              // Other distance
                        0,              // Vertex distance
                        0,              // SetbackDistance1
                        0               // SetbackDistance2
                    ) as Feature;

                    if (feature != null)
                    {
                        result.Success = true;
                        result.Message = "倒角创建成功";
                        result.FeatureName = feature.Name;
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = "倒角创建失败，请先选择边线";
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"倒角创建失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 抽壳特征
        /// </summary>
        /// <param name="thickness">壳体厚度 (米)</param>
        /// <param name="outward">是否向外抽壳</param>
        /// <returns>操作结果</returns>
        public async Task<FeatureResult> Shell(double thickness, bool outward = false)
        {
            var result = new FeatureResult();

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

                    // InsertFeatureShell - 使用前需要先选择要移除的面 (Mark=1)
                    // 如果没有选择面，则创建完全封闭的抽壳
                    model.InsertFeatureShell(thickness, outward);
                    
                    // 检查是否成功创建
                    var selMgr = model.SelectionManager as SelectionMgr;
                    var selectedCount = selMgr.GetSelectedObjectCount2(-1);
                    
                    // 获取最后一个特征
                    var featMgr = model.FeatureManager;
                    var features = featMgr.GetFeatures(false) as object[];
                    if (features != null && features.Length > 0)
                    {
                        var lastFeature = features[features.Length - 1] as Feature;
                        if (lastFeature != null && lastFeature.GetTypeName2() == "Shell")
                        {
                            result.Success = true;
                            result.Message = "抽壳创建成功";
                            result.FeatureName = lastFeature.Name;
                            return;
                        }
                    }
                    
                    result.Success = true;
                    result.Message = "抽壳操作已执行，请在 SolidWorks 中确认结果";
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"抽壳创建失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 孔向导特征
        /// </summary>
        /// <param name="holeType">孔类型: simple, counterbore, countersink, tap</param>
        /// <param name="diameter">孔直径 (米)</param>
        /// <param name="depth">孔深度 (米)</param>
        /// <returns>操作结果</returns>
        public async Task<FeatureResult> HoleWizard(string holeType, double diameter, double depth)
        {
            var result = new FeatureResult();

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

                    var featMgr = model.FeatureManager;
                    
                    // 确定孔类型
                    int generalHoleType;
                    switch (holeType.ToLower())
                    {
                        case "counterbore":
                            generalHoleType = (int)swWzdGeneralHoleTypes_e.swWzdCounterBore;
                            break;
                        case "countersink":
                            generalHoleType = (int)swWzdGeneralHoleTypes_e.swWzdCounterSink;
                            break;
                        case "tap":
                            generalHoleType = (int)swWzdGeneralHoleTypes_e.swWzdTap;
                            break;
                        case "simple":
                        default:
                            generalHoleType = (int)swWzdGeneralHoleTypes_e.swWzdHole;
                            break;
                    }
                    
                    // HoleWizard5 - 27个参数
                    // 使用前需要先使用 SelectByRay 选择放置点 (Mark=0)
                    // 默认使用 ANSI Inch 标准
                    var feature = featMgr.HoleWizard5(
                        generalHoleType,    // GenericHoleType - 孔类型
                        (int)swWzdHoleStandards_e.swStandardAnsiMetric, // StandardIndex - 标准 (ANSI Metric)
                        0,                  // FastenerTypeIndex - 紧固件类型
                        "",                 // SSize - 尺寸字符串
                        (short)swEndConditions_e.swEndCondBlind, // EndType - 终止类型 (盲孔)
                        diameter,           // Diameter - 直径
                        depth,              // Depth - 深度
                        -1,                 // Length - 长度 (仅槽孔有效)
                        -1,                 // Value1
                        -1,                 // Value2
                        -1,                 // Value3
                        -1,                 // Value4
                        -1,                 // Value5
                        -1,                 // Value6
                        -1,                 // Value7
                        -1,                 // Value8
                        -1,                 // Value9
                        -1,                 // Value10
                        -1,                 // Value11
                        -1,                 // Value12
                        "",                 // ThreadClass - 螺纹等级
                        false,              // RevDir - 反向
                        true,               // FeatureScope - 特征范围
                        false,              // AutoSelect - 自动选择
                        false,              // AssemblyFeatureScope - 装配体特征范围
                        false,              // AutoSelectComponents - 自动选择零部件
                        false               // PropagateFeatureToParts - 传播特征到零件
                    ) as Feature;

                    if (feature != null)
                    {
                        result.Success = true;
                        result.Message = "孔向导创建成功";
                        result.FeatureName = feature.Name;
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = "孔向导创建失败，请确保已选择放置面上的点位置";
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"孔向导创建失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 线性阵列特征
        /// </summary>
        /// <param name="direction1Count">方向1数量</param>
        /// <param name="direction1Spacing">方向1间距 (米)</param>
        /// <param name="direction2Count">方向2数量</param>
        /// <param name="direction2Spacing">方向2间距 (米)</param>
        /// <returns>操作结果</returns>
        public async Task<FeatureResult> LinearPattern(int direction1Count, double direction1Spacing, int direction2Count = 1, double direction2Spacing = 0)
        {
            var result = new FeatureResult();

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

                    var featMgr = model.FeatureManager;
                    
                    // 使用 FeatureLinearPattern3 (更简单的版本)
                    var feature = featMgr.FeatureLinearPattern3(
                        direction1Count,    // D1TotalInstances
                        direction1Spacing,  // D1Spacing
                        direction2Count,    // D2TotalInstances
                        direction2Spacing,  // D2Spacing
                        true,               // D1ReverseDirection
                        false,              // D2ReverseDirection
                        null,               // D1PatternSeedFeatureName
                        null,               // D2PatternSeedFeatureName
                        false,              // GeometryPattern
                        true                // VarySketch
                    ) as Feature;

                    if (feature != null)
                    {
                        result.Success = true;
                        result.Message = "线性阵列创建成功";
                        result.FeatureName = feature.Name;
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = "线性阵列创建失败，请先选择要阵列的特征和方向";
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"线性阵列创建失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 圆周阵列特征
        /// </summary>
        /// <param name="count">实例数量</param>
        /// <param name="angle">总角度 (度)，360为完整圆周</param>
        /// <param name="equalSpacing">是否等间距</param>
        /// <returns>操作结果</returns>
        public async Task<FeatureResult> CircularPattern(int count, double angle = 360, bool equalSpacing = true)
        {
            var result = new FeatureResult();

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

                    var featMgr = model.FeatureManager;
                    double angleRad = angle * Math.PI / 180.0;
                    
                    // 使用 FeatureCircularPattern3 (更简单的版本)
                    var feature = featMgr.FeatureCircularPattern3(
                        count,              // NumberOfInstances
                        angleRad,           // Spacing (total angle)
                        false,              // FlipDirection
                        null,               // PatternSeedFeatureName
                        false,              // GeometryPattern
                        true                // EqualSpacing
                    ) as Feature;

                    if (feature != null)
                    {
                        result.Success = true;
                        result.Message = "圆周阵列创建成功";
                        result.FeatureName = feature.Name;
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = "圆周阵列创建失败，请先选择要阵列的特征和旋转轴";
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"圆周阵列创建失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 镜像特征
        /// </summary>
        /// <returns>操作结果</returns>
        public async Task<FeatureResult> Mirror()
        {
            var result = new FeatureResult();

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

                    var featMgr = model.FeatureManager;
                    
                    // 使用 InsertMirrorFeature (需要4个参数)
                    var feature = featMgr.InsertMirrorFeature(
                        false,              // MirrorBody
                        false,              // GeometryPattern
                        true,               // MergeSmooth
                        false               // BKnit
                    ) as Feature;

                    if (feature != null)
                    {
                        result.Success = true;
                        result.Message = "镜像创建成功";
                        result.FeatureName = feature.Name;
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = "镜像创建失败，请先选择要镜像的特征和镜像平面";
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"镜像创建失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 创建基准平面
        /// </summary>
        /// <param name="offsetDistance">偏移距离 (米)</param>
        /// <returns>操作结果</returns>
        public async Task<FeatureResult> CreateReferencePlane(double offsetDistance)
        {
            var result = new FeatureResult();

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

                    var featMgr = model.FeatureManager;
                    
                    // 创建参考平面（基于选择的平面偏移）
                    var feature = featMgr.InsertRefPlane(
                        (int)swRefPlaneReferenceConstraints_e.swRefPlaneReferenceConstraint_Distance,
                        offsetDistance,
                        0, 0,
                        0, 0
                    ) as Feature;

                    if (feature != null)
                    {
                        result.Success = true;
                        result.Message = "基准平面创建成功";
                        result.FeatureName = feature.Name;
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = "基准平面创建失败，请先选择参考面";
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"基准平面创建失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 选择面
        /// </summary>
        /// <param name="faceName">面名称或Face<n>格式</param>
        /// <returns>操作结果</returns>
        public async Task<FeatureResult> SelectFace(string faceName)
        {
            var result = new FeatureResult();

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

                    bool selected = model.Extension.SelectByID2(faceName, "FACE", 0, 0, 0, false, 0, null, 0);
                    
                    if (selected)
                    {
                        result.Success = true;
                        result.Message = $"面 {faceName} 选择成功";
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = $"无法选择面: {faceName}";
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
        /// 选择边线
        /// </summary>
        /// <param name="edgeName">边线名称或Edge<n>格式</param>
        /// <param name="append">是否追加选择</param>
        /// <returns>操作结果</returns>
        public async Task<FeatureResult> SelectEdge(string edgeName, bool append = false)
        {
            var result = new FeatureResult();

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

                    bool selected = model.Extension.SelectByID2(edgeName, "EDGE", 0, 0, 0, append, 0, null, 0);
                    
                    if (selected)
                    {
                        result.Success = true;
                        result.Message = $"边线 {edgeName} 选择成功";
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = $"无法选择边线: {edgeName}";
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

        /// <summary>
        /// 选择特征
        /// </summary>
        /// <param name="featureName">特征名称</param>
        /// <param name="append">是否追加选择</param>
        /// <returns>操作结果</returns>
        public async Task<FeatureResult> SelectFeature(string featureName, bool append = false)
        {
            var result = new FeatureResult();

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

                    bool selected = model.Extension.SelectByID2(featureName, "BODYFEATURE", 0, 0, 0, append, 0, null, 0);
                    
                    if (selected)
                    {
                        result.Success = true;
                        result.Message = $"特征 {featureName} 选择成功";
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = $"无法选择特征: {featureName}";
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"选择特征失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 清除所有选择
        /// </summary>
        /// <returns>操作结果</returns>
        public async Task<FeatureResult> ClearSelection()
        {
            var result = new FeatureResult();

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

                    model.ClearSelection2(true);
                    result.Success = true;
                    result.Message = "选择已清除";
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"清除选择失败: {ex.Message}";
                }
            });

            return result;
        }
    }

    /// <summary>
    /// 特征操作结果
    /// </summary>
    public class FeatureResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string FeatureName { get; set; }
    }
}
