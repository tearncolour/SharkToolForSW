using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Newtonsoft.Json.Linq;

namespace SharkTools
{
    public class ModelConverter
    {
        private readonly ISldWorks _swApp;
        private readonly Func<Action, Task> _uiInvoker;
        
        // 静态锁确保同一时间只有一个转换在进行
        private static readonly SemaphoreSlim _conversionLock = new SemaphoreSlim(1, 1);

        public ModelConverter(ISldWorks swApp, Func<Action, Task> uiInvoker)
        {
            _swApp = swApp;
            _uiInvoker = uiInvoker;
        }

        private void Log(string msg)
        {
            try {
                File.AppendAllText(@"c:\Users\Administrator\Desktop\SharkToolForSW\debug_log.txt", 
                    $"{DateTime.Now}: ModelConverter - {msg}\r\n", System.Text.Encoding.UTF8);
            } catch {}
        }

        public async Task<object> ConvertAsync(string filePath, JToken options)
        {
            // 先检查文件是否存在
            Log($"Received path: {filePath}");
            Log($"Path bytes: {BitConverter.ToString(System.Text.Encoding.UTF8.GetBytes(filePath))}");
            
            if (!File.Exists(filePath))
            {
                Log($"File does not exist: {filePath}");
                return new { success = false, message = $"文件不存在: {filePath}" };
            }
            Log($"File exists: {filePath}");
            // 等待获取锁（确保串行转换）
            Log($"Waiting for conversion lock: {filePath}");
            await _conversionLock.WaitAsync();
            Log($"Acquired conversion lock: {filePath}");
            
            try
            {
                return await ConvertInternalAsync(filePath, options);
            }
            finally
            {
                _conversionLock.Release();
                Log($"Released conversion lock: {filePath}");
            }
        }
        
        private async Task<object> ConvertInternalAsync(string filePath, JToken options)
        {
            string newPath = Path.ChangeExtension(filePath, ".sldprt");
            object result = null;

            Log($"Starting conversion for {filePath}");

            await _uiInvoker(() =>
            {
                // 保存当前用户设置
                bool originalUserControlBackground = false;
                bool wasVisible = true;
                HashSet<string> existingDocs = new HashSet<string>();
                
                try
                {
                    Log("Inside UI Thread");
                    int errors = 0;

                    // 记录转换前已经打开的文档，避免关闭用户的文档
                    object docNames = _swApp.GetDocuments();
                    if (docNames != null && docNames is object[] docs)
                    {
                        foreach (var d in docs)
                        {
                            ModelDoc2 existingDoc = d as ModelDoc2;
                            if (existingDoc != null)
                            {
                                existingDocs.Add(existingDoc.GetTitle());
                                Log($"Existing doc: {existingDoc.GetTitle()}");
                            }
                        }
                    }
                    Log($"Found {existingDocs.Count} existing documents");

                    // 设置静默模式 - 禁止显示对话框和消息
                    originalUserControlBackground = _swApp.UserControlBackground;
                    wasVisible = _swApp.Visible;
                    
                    // 设置用户控制后台模式，防止弹出对话框
                    _swApp.UserControlBackground = true;
                    
                    // 禁用所有对话框和警告
                    _swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swInputDimValOnCreate, false);

                    Log("Opening STEP file with LoadFile4...");
                    
                    // 使用 LoadFile4 导入 STEP 文件（这是导入外部格式的正确方法）
                    ImportStepData importData = (ImportStepData)_swApp.GetImportFileData(filePath);
                    if (importData != null)
                    {
                        // 设置导入选项：导入为单个零件体
                        importData.MapConfigurationData = true;
                        Log("ImportData configured");
                    }
                    
                    ModelDoc2 swModel = _swApp.LoadFile4(
                        filePath,
                        "r",  // 只读模式
                        importData,
                        ref errors);

                    if (swModel == null)
                    {
                        Log($"LoadFile4 failed. Errors: {errors} (0x{errors:X})");
                        throw new Exception($"无法打开文件: {filePath} (Error: {errors}, Hex: 0x{errors:X})");
                    }
                    Log($"Doc Opened: {swModel.GetTitle()}");

                    // 2. Save as SLDPRT
                    Log($"Saving to {newPath}...");
                    
                    // 使用 SaveAs3 带更多控制选项
                    int saveErrors = 0;
                    int saveWarnings = 0;
                    bool saved = swModel.Extension.SaveAs3(
                        newPath, 
                        (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                        (int)swSaveAsOptions_e.swSaveAsOptions_Silent | (int)swSaveAsOptions_e.swSaveAsOptions_Copy,
                        null, 
                        null,
                        ref saveErrors, 
                        ref saveWarnings);

                    if (!saved)
                    {
                        Log($"SaveAs failed. Errors: {saveErrors}");
                        _swApp.CloseDoc(swModel.GetTitle());
                        throw new Exception($"保存为 SLDPRT 失败: {saveErrors}");
                    }
                    Log("Saved successfully.");

                    // 3. Feature Recognition (Optional)
                    string message = "文件已转换为 " + Path.GetFileName(newPath);
                    if (options != null)
                    {
                        message += " (特征识别已跳过)";
                    }

                    // 关闭所有新打开的文档（保留用户原有的文档）
                    Log("Closing newly opened documents...");
                    try
                    {
                        // 获取当前所有打开的文档
                        object currentDocs = _swApp.GetDocuments();
                        if (currentDocs != null && currentDocs is object[] allDocs)
                        {
                            List<string> docsToClose = new List<string>();
                            
                            // 找出所有需要关闭的文档（转换前不存在的）
                            foreach (var d in allDocs)
                            {
                                ModelDoc2 doc = d as ModelDoc2;
                                if (doc != null)
                                {
                                    string title = doc.GetTitle();
                                    if (!existingDocs.Contains(title))
                                    {
                                        docsToClose.Add(title);
                                        Log($"Will close: {title}");
                                    }
                                }
                            }
                            
                            // 关闭所有新打开的文档
                            foreach (string title in docsToClose)
                            {
                                try
                                {
                                    _swApp.CloseDoc(title);
                                    Log($"Closed: {title}");
                                }
                                catch (Exception closeEx)
                                {
                                    Log($"Failed to close {title}: {closeEx.Message}");
                                }
                            }
                            
                            Log($"Closed {docsToClose.Count} new documents");
                        }
                    }
                    catch (Exception closeEx)
                    {
                        Log($"Close error (non-fatal): {closeEx.Message}");
                    }

                    result = new { success = true, newPath = newPath, message = message };
                }
                catch (Exception ex)
                {
                    Log($"Error: {ex.Message}\n{ex.StackTrace}");
                    result = new { success = false, message = ex.Message };
                }
                finally
                {
                    // 恢复用户设置
                    try
                    {
                        _swApp.UserControlBackground = originalUserControlBackground;
                    }
                    catch { }
                }
            });

            return result;
        }
    }
}
