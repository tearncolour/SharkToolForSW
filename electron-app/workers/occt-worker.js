const { parentPort, workerData } = require('worker_threads');
const fs = require('fs');
const path = require('path');
const occtimportjs = require('occt-import-js');

(async () => {
    try {
        const { filePath, params } = workerData;
        
        // 初始化 OCCT
        const occt = await occtimportjs();
        
        // 读取文件
        const buffer = await fs.promises.readFile(filePath);
        const ext = path.extname(filePath).toLowerCase();

        let result;
        
        // 执行转换
        if (ext === '.step' || ext === '.stp') {
            result = occt.ReadStepFile(buffer, params);
        } else if (ext === '.iges' || ext === '.igs') {
            result = occt.ReadIgesFile(buffer, params);
        } else if (ext === '.brep') {
            result = occt.ReadBrepFile(buffer, params);
        } else {
            result = occt.ReadStepFile(buffer, params);
        }

        // 发送结果
        parentPort.postMessage({ success: true, meshes: result.meshes });
    } catch (error) {
        parentPort.postMessage({ success: false, message: error.message });
    }
})();