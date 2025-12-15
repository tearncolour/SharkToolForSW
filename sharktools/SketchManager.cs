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
    /// 草图管理器 - 提供草图绘制相关功能
    /// 包含创建草图、绘制几何图形、添加约束等操作
    /// </summary>
    public class SharkSketchManager
    {
        private readonly ISldWorks _swApp;
        private readonly Func<Action, Task> _runOnUIThread;

        public SharkSketchManager(ISldWorks swApp, Func<Action, Task> runOnUIThread)
        {
            _swApp = swApp;
            _runOnUIThread = runOnUIThread;
        }

        /// <summary>
        /// 在指定平面上创建新草图
        /// </summary>
        /// <param name="planeName">平面名称：Front Plane, Top Plane, Right Plane 或自定义平面名</param>
        /// <returns>操作结果</returns>
        public async Task<SketchResult> CreateSketch(string planeName = "Front Plane")
        {
            var result = new SketchResult();
            
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

                    // 查找平面 Feature（避免使用 SelectByID2 导致卡死）
                    Feature planeFeature = null;
                    Feature feat = model.FirstFeature() as Feature;
                    
                    string searchName = planeName.ToLower().Replace(" ", "");
                    
                    while (feat != null)
                    {
                        string featName = feat.Name.ToLower().Replace(" ", "");
                        string featType = feat.GetTypeName2();
                        
                        // 检查是否是基准面
                        if (featType == "RefPlane")
                        {
                            // 匹配英文名
                            if (featName == searchName ||
                                (searchName == "frontplane" && featName == "frontplane") ||
                                (searchName == "topplane" && featName == "topplane") ||
                                (searchName == "rightplane" && featName == "rightplane") ||
                                // 匹配中文名
                                (searchName == "前视基准面" && (featName.Contains("front") || featName.Contains("前"))) ||
                                (searchName == "上视基准面" && (featName.Contains("top") || featName.Contains("上"))) ||
                                (searchName == "右视基准面" && (featName.Contains("right") || featName.Contains("右"))))
                            {
                                planeFeature = feat;
                                break;
                            }
                        }
                        
                        feat = feat.GetNextFeature() as Feature;
                    }

                    if (planeFeature == null)
                    {
                        result.Success = false;
                        result.Message = $"找不到平面: {planeName}";
                        return;
                    }

                    // 直接在平面上创建草图（不使用 SelectByID2）
                    var sketchMgr = model.SketchManager as SketchManager;
                    
                    // 清除所有选择
                    model.ClearSelection2(true);
                    
                    // 选择平面特征
                    bool selected = planeFeature.Select2(false, 0);
                    if (!selected)
                    {
                        result.Success = false;
                        result.Message = $"无法选择平面特征: {planeName}";
                        return;
                    }
                    
                    // 创建草图
                    sketchMgr.InsertSketch(true);
                    
                    var activeSketch = model.GetActiveSketch2() as Sketch;
                    if (activeSketch != null)
                    {
                        result.Success = true;
                        result.Message = "草图创建成功";
                        result.SketchName = "Sketch";
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = "草图创建失败";
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"创建草图失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 绘制直线
        /// </summary>
        /// <param name="x1">起点X坐标 (米)</param>
        /// <param name="y1">起点Y坐标 (米)</param>
        /// <param name="x2">终点X坐标 (米)</param>
        /// <param name="y2">终点Y坐标 (米)</param>
        /// <returns>操作结果</returns>
        public async Task<SketchResult> DrawLine(double x1, double y1, double x2, double y2)
        {
            var result = new SketchResult();

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

                    var sketchMgr = model.SketchManager;
                    
                    // 检查是否在草图编辑模式
                    if (model.GetActiveSketch2() == null)
                    {
                        result.Success = false;
                        result.Message = "请先进入草图编辑模式";
                        return;
                    }

                    // 绘制直线 (坐标单位为米)
                    var line = sketchMgr.CreateLine(x1, y1, 0, x2, y2, 0);
                    
                    if (line != null)
                    {
                        result.Success = true;
                        result.Message = "直线绘制成功";
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = "直线绘制失败";
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"绘制直线失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 绘制矩形
        /// </summary>
        /// <param name="x1">左下角X坐标 (米)</param>
        /// <param name="y1">左下角Y坐标 (米)</param>
        /// <param name="x2">右上角X坐标 (米)</param>
        /// <param name="y2">右上角Y坐标 (米)</param>
        /// <returns>操作结果</returns>
        public async Task<SketchResult> DrawRectangle(double x1, double y1, double x2, double y2)
        {
            var result = new SketchResult();

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

                    var sketchMgr = model.SketchManager;
                    
                    if (model.GetActiveSketch2() == null)
                    {
                        result.Success = false;
                        result.Message = "请先进入草图编辑模式";
                        return;
                    }

                    // 绘制角点矩形
                    var segments = sketchMgr.CreateCornerRectangle(x1, y1, 0, x2, y2, 0);
                    
                    if (segments != null)
                    {
                        result.Success = true;
                        result.Message = "矩形绘制成功";
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = "矩形绘制失败";
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"绘制矩形失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 绘制圆形
        /// </summary>
        /// <param name="centerX">圆心X坐标 (米)</param>
        /// <param name="centerY">圆心Y坐标 (米)</param>
        /// <param name="radius">半径 (米)</param>
        /// <returns>操作结果</returns>
        public async Task<SketchResult> DrawCircle(double centerX, double centerY, double radius)
        {
            var result = new SketchResult();

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

                    var sketchMgr = model.SketchManager;
                    
                    if (model.GetActiveSketch2() == null)
                    {
                        result.Success = false;
                        result.Message = "请先进入草图编辑模式";
                        return;
                    }

                    // 绘制圆
                    var circle = sketchMgr.CreateCircle(centerX, centerY, 0, centerX + radius, centerY, 0);
                    
                    if (circle != null)
                    {
                        result.Success = true;
                        result.Message = "圆形绘制成功";
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = "圆形绘制失败";
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"绘制圆形失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 绘制圆弧
        /// </summary>
        /// <param name="centerX">圆心X坐标 (米)</param>
        /// <param name="centerY">圆心Y坐标 (米)</param>
        /// <param name="startX">起点X坐标 (米)</param>
        /// <param name="startY">起点Y坐标 (米)</param>
        /// <param name="endX">终点X坐标 (米)</param>
        /// <param name="endY">终点Y坐标 (米)</param>
        /// <returns>操作结果</returns>
        public async Task<SketchResult> DrawArc(double centerX, double centerY, double startX, double startY, double endX, double endY)
        {
            var result = new SketchResult();

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

                    var sketchMgr = model.SketchManager;
                    
                    if (model.GetActiveSketch2() == null)
                    {
                        result.Success = false;
                        result.Message = "请先进入草图编辑模式";
                        return;
                    }

                    // 绘制圆弧 (圆心-起点-终点方式)
                    var arc = sketchMgr.CreateArc(centerX, centerY, 0, startX, startY, 0, endX, endY, 0, 1);
                    
                    if (arc != null)
                    {
                        result.Success = true;
                        result.Message = "圆弧绘制成功";
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = "圆弧绘制失败";
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"绘制圆弧失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 绘制样条曲线
        /// </summary>
        /// <param name="points">点坐标数组，格式: [[x1,y1], [x2,y2], ...]</param>
        /// <returns>操作结果</returns>
        public async Task<SketchResult> DrawSpline(double[][] points)
        {
            var result = new SketchResult();

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

                    var sketchMgr = model.SketchManager;
                    
                    if (model.GetActiveSketch2() == null)
                    {
                        result.Success = false;
                        result.Message = "请先进入草图编辑模式";
                        return;
                    }

                    if (points == null || points.Length < 2)
                    {
                        result.Success = false;
                        result.Message = "样条曲线至少需要2个点";
                        return;
                    }

                    // 构建点数组 (x,y,z格式)
                    var pointArray = new double[points.Length * 3];
                    for (int i = 0; i < points.Length; i++)
                    {
                        pointArray[i * 3] = points[i][0];
                        pointArray[i * 3 + 1] = points[i][1];
                        pointArray[i * 3 + 2] = 0; // Z = 0
                    }

                    // 绘制样条曲线
                    var spline = sketchMgr.CreateSpline2(pointArray, true);
                    
                    if (spline != null)
                    {
                        result.Success = true;
                        result.Message = "样条曲线绘制成功";
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = "样条曲线绘制失败";
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"绘制样条曲线失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 绘制多边形
        /// </summary>
        /// <param name="centerX">中心X坐标 (米)</param>
        /// <param name="centerY">中心Y坐标 (米)</param>
        /// <param name="radius">外接圆半径 (米)</param>
        /// <param name="sides">边数 (3-100)</param>
        /// <returns>操作结果</returns>
        public async Task<SketchResult> DrawPolygon(double centerX, double centerY, double radius, int sides = 6)
        {
            var result = new SketchResult();

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

                    var sketchMgr = model.SketchManager;
                    
                    if (model.GetActiveSketch2() == null)
                    {
                        result.Success = false;
                        result.Message = "请先进入草图编辑模式";
                        return;
                    }

                    if (sides < 3 || sides > 100)
                    {
                        result.Success = false;
                        result.Message = "边数必须在3到100之间";
                        return;
                    }

                    // 绘制正多边形
                    var polygon = sketchMgr.CreatePolygon(centerX, centerY, 0, centerX + radius, centerY, 0, sides, false);
                    
                    if (polygon != null)
                    {
                        result.Success = true;
                        result.Message = $"{sides}边形绘制成功";
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = "多边形绘制失败";
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"绘制多边形失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 绘制椭圆
        /// </summary>
        /// <param name="centerX">中心X坐标 (米)</param>
        /// <param name="centerY">中心Y坐标 (米)</param>
        /// <param name="majorRadius">长轴半径 (米)</param>
        /// <param name="minorRadius">短轴半径 (米)</param>
        /// <returns>操作结果</returns>
        public async Task<SketchResult> DrawEllipse(double centerX, double centerY, double majorRadius, double minorRadius)
        {
            var result = new SketchResult();

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

                    var sketchMgr = model.SketchManager;
                    
                    if (model.GetActiveSketch2() == null)
                    {
                        result.Success = false;
                        result.Message = "请先进入草图编辑模式";
                        return;
                    }

                    // 绘制椭圆
                    var ellipse = sketchMgr.CreateEllipse(
                        centerX, centerY, 0,                    // 中心点
                        centerX + majorRadius, centerY, 0,      // 长轴端点
                        centerX, centerY + minorRadius, 0       // 短轴端点
                    );
                    
                    if (ellipse != null)
                    {
                        result.Success = true;
                        result.Message = "椭圆绘制成功";
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = "椭圆绘制失败";
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"绘制椭圆失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 绘制槽口 (圆端直槽)
        /// </summary>
        /// <param name="x1">起点圆心X坐标 (米)</param>
        /// <param name="y1">起点圆心Y坐标 (米)</param>
        /// <param name="x2">终点圆心X坐标 (米)</param>
        /// <param name="y2">终点圆心Y坐标 (米)</param>
        /// <param name="width">槽口宽度 (米)</param>
        /// <returns>操作结果</returns>
        public async Task<SketchResult> DrawSlot(double x1, double y1, double x2, double y2, double width)
        {
            var result = new SketchResult();

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

                    var sketchMgr = model.SketchManager;
                    
                    if (model.GetActiveSketch2() == null)
                    {
                        result.Success = false;
                        result.Message = "请先进入草图编辑模式";
                        return;
                    }

                    // 绘制直槽口
                    var slot = sketchMgr.CreateSketchSlot(
                        (int)swSketchSlotCreationType_e.swSketchSlotCreationType_line,  // 直线槽口
                        (int)swSketchSlotLengthType_e.swSketchSlotLengthType_CenterCenter,  // 中心到中心
                        width,       // 宽度
                        x1, y1, 0,   // 起点
                        x2, y2, 0,   // 终点
                        0, 0, 0,     // 未使用 (弧形槽口用)
                        1, true      // 方向向量X, 添加尺寸约束
                    );
                    
                    if (slot != null)
                    {
                        result.Success = true;
                        result.Message = "槽口绘制成功";
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = "槽口绘制失败";
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"绘制槽口失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 添加草图约束
        /// </summary>
        /// <param name="constraintType">约束类型: horizontal, vertical, coincident, concentric, etc.</param>
        /// <returns>操作结果</returns>
        public async Task<SketchResult> AddConstraint(string constraintType)
        {
            var result = new SketchResult();

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

                    if (model.GetActiveSketch2() == null)
                    {
                        result.Success = false;
                        result.Message = "请先进入草图编辑模式";
                        return;
                    }

                    // 获取选择管理器
                    var selMgr = model.SelectionManager as SelectionMgr;
                    if (selMgr.GetSelectedObjectCount2(-1) == 0)
                    {
                        result.Success = false;
                        result.Message = "请先选择草图实体";
                        return;
                    }

                    // 根据类型添加约束
                    int constraintId = GetConstraintType(constraintType);
                    if (constraintId < 0)
                    {
                        result.Success = false;
                        result.Message = $"未知的约束类型: {constraintType}";
                        return;
                    }

                    model.SketchAddConstraints(constraintType);
                    result.Success = true;
                    result.Message = $"约束 {constraintType} 添加成功";
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"添加约束失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 添加尺寸标注
        /// </summary>
        /// <param name="x">标注放置X位置 (米)</param>
        /// <param name="y">标注放置Y位置 (米)</param>
        /// <param name="value">尺寸值 (米，可选，如果提供则设置驱动尺寸)</param>
        /// <returns>操作结果</returns>
        public async Task<SketchResult> AddDimension(double x, double y, double? value = null)
        {
            var result = new SketchResult();

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

                    if (model.GetActiveSketch2() == null)
                    {
                        result.Success = false;
                        result.Message = "请先进入草图编辑模式";
                        return;
                    }

                    var selMgr = model.SelectionManager as SelectionMgr;
                    if (selMgr.GetSelectedObjectCount2(-1) == 0)
                    {
                        result.Success = false;
                        result.Message = "请先选择草图实体";
                        return;
                    }

                    // 添加尺寸
                    var dim = model.AddDimension2(x, y, 0) as DisplayDimension;
                    
                    if (dim != null)
                    {
                        // 如果提供了值，设置尺寸
                        if (value.HasValue)
                        {
                            var dimension = dim.GetDimension2(0) as Dimension;
                            if (dimension != null)
                            {
                                dimension.SetSystemValue3(value.Value, (int)swSetValueInConfiguration_e.swSetValue_InThisConfiguration, null);
                            }
                        }
                        
                        result.Success = true;
                        result.Message = "尺寸标注添加成功";
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = "尺寸标注添加失败";
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"添加尺寸失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 退出草图编辑模式
        /// </summary>
        /// <returns>操作结果</returns>
        public async Task<SketchResult> ExitSketch()
        {
            var result = new SketchResult();

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

                    var sketchMgr = model.SketchManager;
                    sketchMgr.InsertSketch(true); // 再次调用会退出草图

                    result.Success = true;
                    result.Message = "已退出草图编辑模式";
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"退出草图失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 获取当前草图信息
        /// </summary>
        /// <returns>草图信息</returns>
        public async Task<SketchInfoResult> GetSketchInfo()
        {
            var result = new SketchInfoResult();

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

                    var activeSketch = model.GetActiveSketch2() as Sketch;
                    if (activeSketch == null)
                    {
                        result.Success = false;
                        result.Message = "当前不在草图编辑模式";
                        result.IsInSketchMode = false;
                        return;
                    }

                    result.Success = true;
                    result.IsInSketchMode = true;
                    result.SketchName = "ActiveSketch";
                    
                    // 获取草图统计信息
                    var segments = activeSketch.GetSketchSegments() as object[];
                    result.SegmentCount = segments?.Length ?? 0;
                    
                    var points = activeSketch.GetSketchPoints2() as object[];
                    result.PointCount = points?.Length ?? 0;
                    
                    result.RelationCount = 0;
                    
                    result.Message = "草图信息获取成功";
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"获取草图信息失败: {ex.Message}";
                }
            });

            return result;
        }

        /// <summary>
        /// 将约束类型字符串转换为枚举值
        /// </summary>
        private int GetConstraintType(string type)
        {
            switch (type.ToLower())
            {
                case "horizontal": return (int)swConstraintType_e.swConstraintType_HORIZONTAL;
                case "vertical": return (int)swConstraintType_e.swConstraintType_VERTICAL;
                case "coincident": return (int)swConstraintType_e.swConstraintType_COINCIDENT;
                case "concentric": return (int)swConstraintType_e.swConstraintType_CONCENTRIC;
                case "perpendicular": return (int)swConstraintType_e.swConstraintType_PERPENDICULAR;
                case "parallel": return (int)swConstraintType_e.swConstraintType_PARALLEL;
                case "tangent": return (int)swConstraintType_e.swConstraintType_TANGENT;
                case "equal": return (int)swConstraintType_e.swConstraintType_SAMELENGTH;
                case "midpoint": return (int)swConstraintType_e.swConstraintType_ATMIDDLE;
                case "fix": return (int)swConstraintType_e.swConstraintType_FIXED;
                case "collinear": return (int)swConstraintType_e.swConstraintType_COLINEAR;
                default: return -1;
            }
        }
    }

    /// <summary>
    /// 草图操作结果
    /// </summary>
    public class SketchResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string SketchName { get; set; }
    }

    /// <summary>
    /// 草图信息结果
    /// </summary>
    public class SketchInfoResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public bool IsInSketchMode { get; set; }
        public string SketchName { get; set; }
        public int SegmentCount { get; set; }
        public int PointCount { get; set; }
        public int RelationCount { get; set; }
    }
}
