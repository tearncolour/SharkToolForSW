# SharkTools 项目文档

## 目录

1. [项目概述](docs/1-项目概述.md)
2. [系统架构](docs/2-系统架构.md)
3. [插件端 (SolidWorks Add-in)](docs/3-插件端.md)
4. [客户端 (Electron 应用)](docs/4-客户端.md)
5. [通信协议](docs/5-通信协议.md)
6. [功能模块](docs/6-功能模块.md)
7. [数据存储](docs/7-数据存储.md)
8. [安装与部署](docs/8-安装与部署.md)
9. [开发指南](docs/9-开发指南.md)
10. [API 参考](docs/10-API参考.md)
11. [工程文件结构说明](docs/工程文件结构说明.md)
12. [附录](docs/附录.md)

---

## 工程介绍

SharkTools 是一个面向 SolidWorks 用户的辅助工具套件，由两部分组成：

- **SolidWorks 插件端**：以 COM Add-in 形式集成到 SolidWorks 中，提供底层 API 访问能力
- **Electron 客户端**：独立桌面应用程序，提供现代化的用户界面和扩展功能

### 主要功能

- 文件资源管理器（支持预览 SolidWorks 文件缩略图）
- 历史记录追踪与回滚
- 自定义属性管理（单文件/批量）
- 批量文件重命名
- 项目文件夹管理
- 文件版本对比
- Git 版本控制集成
- STEP/IGES 文件导入转换
- PDF/图片/Excel 文件预览

### 技术栈

| 组件 | 技术 |
|------|------|
| 插件端 | C# / .NET Framework 4.7.2 / COM Interop |
| 客户端 | Electron 28 / Vue 3 / Ant Design Vue 4 |
| 通信层 | WebSocket (ws://127.0.0.1:52789) |
| 数据库 | LiteDB (插件端) / electron-store (客户端) |
| 3D 渲染 | Three.js / occt-import-js |

---

**文档版本**: 1.0  
**最后更新**: 2025-12-12  
**作者**: SharkTools Team