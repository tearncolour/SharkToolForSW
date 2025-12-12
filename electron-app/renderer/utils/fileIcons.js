/**
 * 文件图标映射工具
 * 提供 SolidWorks 和常见文件类型的图标
 */

// 文件类型图标 SVG 路径 (来自 VSCode Material Icon Theme 风格)
export const FILE_ICONS = {
  // SolidWorks 文件
  sldprt: {
    color: '#4fc3f7',
    icon: 'cube', // 零件
  },
  sldasm: {
    color: '#81c784',
    icon: 'cubes', // 装配体
  },
  slddrw: {
    color: '#ffb74d',
    icon: 'file-lines', // 工程图
  },
  sldlfp: {
    color: '#ce93d8',
    icon: 'shapes', // 库特征零件
  },
  sldftp: {
    color: '#ce93d8',
    icon: 'shapes', // 成形工具
  },
  sldblk: {
    color: '#90a4ae',
    icon: 'cube', // 块
  },
  sldprtdot: {
    color: '#4fc3f7',
    icon: 'cube-outline', // 零件模板
  },
  asmdot: {
    color: '#81c784',
    icon: 'cubes-outline', // 装配体模板
  },
  drwdot: {
    color: '#ffb74d',
    icon: 'file-lines-outline', // 工程图模板
  },
  prtdot: {
    color: '#4fc3f7',
    icon: 'cube-outline', // 零件模板
  },
  
  // CAD 交换格式
  step: {
    color: '#26a69a',
    icon: 'exchange',
  },
  stp: {
    color: '#26a69a',
    icon: 'exchange',
  },
  iges: {
    color: '#26a69a',
    icon: 'exchange',
  },
  igs: {
    color: '#26a69a',
    icon: 'exchange',
  },
  stl: {
    color: '#ef5350',
    icon: 'mesh',
  },
  obj: {
    color: '#ab47bc',
    icon: 'mesh',
  },
  x_t: {
    color: '#7e57c2',
    icon: 'exchange',
  },
  x_b: {
    color: '#7e57c2',
    icon: 'exchange',
  },
  sat: {
    color: '#42a5f5',
    icon: 'exchange',
  },
  dxf: {
    color: '#ff7043',
    icon: 'vector-square',
  },
  dwg: {
    color: '#ff7043',
    icon: 'vector-square',
  },
  
  // 图片文件
  png: {
    color: '#66bb6a',
    icon: 'image',
  },
  jpg: {
    color: '#66bb6a',
    icon: 'image',
  },
  jpeg: {
    color: '#66bb6a',
    icon: 'image',
  },
  gif: {
    color: '#66bb6a',
    icon: 'image',
  },
  bmp: {
    color: '#66bb6a',
    icon: 'image',
  },
  svg: {
    color: '#ffb300',
    icon: 'image-vector',
  },
  ico: {
    color: '#66bb6a',
    icon: 'image',
  },
  
  // 文档文件
  pdf: {
    color: '#f44336',
    icon: 'file-pdf',
  },
  doc: {
    color: '#2196f3',
    icon: 'file-word',
  },
  docx: {
    color: '#2196f3',
    icon: 'file-word',
  },
  xls: {
    color: '#4caf50',
    icon: 'file-excel',
  },
  xlsx: {
    color: '#4caf50',
    icon: 'file-excel',
  },
  ppt: {
    color: '#ff5722',
    icon: 'file-powerpoint',
  },
  pptx: {
    color: '#ff5722',
    icon: 'file-powerpoint',
  },
  txt: {
    color: '#90a4ae',
    icon: 'file-text',
  },
  md: {
    color: '#42a5f5',
    icon: 'markdown',
  },
  
  // 代码文件
  js: {
    color: '#ffca28',
    icon: 'file-code',
  },
  ts: {
    color: '#42a5f5',
    icon: 'file-code',
  },
  json: {
    color: '#ffca28',
    icon: 'braces',
  },
  xml: {
    color: '#ff7043',
    icon: 'file-code',
  },
  html: {
    color: '#ff7043',
    icon: 'html5',
  },
  css: {
    color: '#42a5f5',
    icon: 'css3',
  },
  py: {
    color: '#4caf50',
    icon: 'python',
  },
  cs: {
    color: '#9c27b0',
    icon: 'file-code',
  },
  
  // 压缩文件
  zip: {
    color: '#ffa726',
    icon: 'archive',
  },
  rar: {
    color: '#ffa726',
    icon: 'archive',
  },
  '7z': {
    color: '#ffa726',
    icon: 'archive',
  },
  tar: {
    color: '#ffa726',
    icon: 'archive',
  },
  gz: {
    color: '#ffa726',
    icon: 'archive',
  },
  
  // 其他
  exe: {
    color: '#78909c',
    icon: 'application',
  },
  dll: {
    color: '#78909c',
    icon: 'cog',
  },
  bat: {
    color: '#78909c',
    icon: 'terminal',
  },
  ps1: {
    color: '#42a5f5',
    icon: 'terminal',
  },
  shark: {
    color: '#00bcd4',
    icon: 'shark',
  },
}

// 获取文件扩展名
export function getFileExtension(filename) {
  if (!filename || typeof filename !== 'string') return ''
  const parts = filename.split('.')
  if (parts.length < 2) return ''
  return parts.pop().toLowerCase()
}

// 获取文件图标信息
export function getFileIconInfo(filename) {
  const ext = getFileExtension(filename)
  return FILE_ICONS[ext] || { color: '#90a4ae', icon: 'file' }
}

// 获取文件颜色
export function getFileColor(filename) {
  return getFileIconInfo(filename).color
}

// 获取文件图标类型
export function getFileIconType(filename) {
  return getFileIconInfo(filename).icon
}

// 文件夹颜色
export const FOLDER_COLOR = '#dcb67a'

// 默认文件颜色
export const DEFAULT_FILE_COLOR = '#90a4ae'
