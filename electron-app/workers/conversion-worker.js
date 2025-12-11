/**
 * STEP 转换 Worker
 * 严格串行处理转换请求，确保每次只有一个转换在进行
 */

const { parentPort } = require('worker_threads');

// 转换队列
const queue = [];
let isProcessing = false;
let currentResolve = null;

// 日志函数
function log(message) {
    parentPort.postMessage({ type: 'log', message: `[ConversionWorker] ${message}` });
}

// 处理来自主进程的消息
parentPort.on('message', (message) => {
    switch (message.type) {
        case 'add-task':
            // 添加新的转换任务
            queue.push({
                id: message.id,
                filePath: message.filePath,
                options: message.options
            });
            log(`Added task: ${message.filePath} (Queue size: ${queue.length})`);
            processNext();
            break;
            
        case 'conversion-complete':
            // 转换完成，处理下一个
            log(`Conversion complete: ${message.filePath}, success: ${message.success}`);
            if (currentResolve) {
                currentResolve = null;
            }
            isProcessing = false;
            
            // 等待一段时间确保 SW 完全稳定后再处理下一个
            setTimeout(() => {
                processNext();
            }, 2000); // 2秒延迟
            break;
            
        case 'get-status':
            // 返回当前状态
            parentPort.postMessage({
                type: 'status',
                queueSize: queue.length,
                isProcessing: isProcessing
            });
            break;
            
        case 'clear-queue':
            // 清空队列
            queue.length = 0;
            log('Queue cleared');
            break;
    }
});

// 处理下一个任务
function processNext() {
    if (isProcessing || queue.length === 0) {
        if (queue.length === 0) {
            parentPort.postMessage({ type: 'queue-empty' });
        }
        return;
    }
    
    isProcessing = true;
    const task = queue.shift();
    
    log(`Processing: ${task.filePath} (Remaining: ${queue.length})`);
    
    // 通知主进程开始转换
    parentPort.postMessage({
        type: 'start-conversion',
        id: task.id,
        filePath: task.filePath,
        options: task.options,
        remaining: queue.length
    });
}

log('Worker initialized');
