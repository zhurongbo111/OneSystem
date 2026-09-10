import { ref } from 'vue'

/** 订单明细行（例 B 子表格） */
export interface OrderItemRow {
  key: string
  productName: string
  quantity: number
}

/** 订单行数据模型（示例业务：订单，静态 Mock 参照用，非真实业务） */
export interface OrderRow {
  id: string
  orderNo: string
  customer: string
  product: string
  amount: number
  status: 'pending' | 'paid' | 'shipped' | 'cancelled'
  createdAt: string // YYYY-MM-DD
  remark: string
  createdBy: string // 仅详情页展示的只读字段
  updatedAt: string // 仅详情页展示的只读字段
  items: OrderItemRow[]
}

/** 订单表单录入字段（新增/编辑表单共用；不含 id/createdBy/updatedAt 等系统字段） */
export interface OrderFormLike {
  orderNo: string
  customer: string
  product: string
  amount: number | undefined
  status: OrderRow['status']
  createdAt: string
  remark: string
}

/** 订单状态选项与展示映射 */
export const ORDER_STATUS_OPTIONS = [
  { label: '待支付', value: 'pending' },
  { label: '已支付', value: 'paid' },
  { label: '已发货', value: 'shipped' },
  { label: '已取消', value: 'cancelled' },
]

export const ORDER_STATUS_COLOR: Record<OrderRow['status'], string> = {
  pending: 'orange',
  paid: 'arcoblue',
  shipped: 'green',
  cancelled: 'gray',
}

export const ORDER_STATUS_LABEL: Record<OrderRow['status'], string> = {
  pending: '待支付',
  paid: '已支付',
  shipped: '已发货',
  cancelled: '已取消',
}

/** 格式化金额：¥ 前缀 + 两位小数 */
export function formatAmount(n: number): string {
  return `¥ ${n.toFixed(2)}`
}

function cloneRows(list: OrderRow[]): OrderRow[] {
  return list.map((r) => ({ ...r, items: r.items.map((i) => ({ ...i })) }))
}

/** 静态种子数据：跨状态/金额/时间，便于验证筛选与排序 */
const SEED: OrderRow[] = [
  { id: 'o01', orderNo: 'NO-20250108-001', customer: '张伟', product: '机械键盘', amount: 399, status: 'paid', createdAt: '2025-01-08', remark: '尽快发货', createdBy: 'admin', updatedAt: '2025-01-09', items: [{ key: 'i1', productName: '机械键盘', quantity: 1 }] },
  { id: 'o02', orderNo: 'NO-20250115-002', customer: '李娜', product: '无线鼠标', amount: 129.5, status: 'shipped', createdAt: '2025-01-15', remark: '', createdBy: 'admin', updatedAt: '2025-01-18', items: [{ key: 'i1', productName: '无线鼠标', quantity: 1 }, { key: 'i2', productName: '鼠标垫', quantity: 2 }] },
  { id: 'o03', orderNo: 'NO-20250203-003', customer: '王强', product: '显示器支架', amount: 259, status: 'pending', createdAt: '2025-02-03', remark: '含安装', createdBy: 'admin', updatedAt: '2025-02-03', items: [{ key: 'i1', productName: '显示器支架', quantity: 2 }] },
  { id: 'o04', orderNo: 'NO-20250219-004', customer: '刘敏', product: 'USB 集线器', amount: 89, status: 'cancelled', createdAt: '2025-02-19', remark: '重复下单已取消', createdBy: 'admin', updatedAt: '2025-02-20', items: [{ key: 'i1', productName: 'USB 集线器', quantity: 1 }] },
  { id: 'o05', orderNo: 'NO-20250302-005', customer: '陈杰', product: '降噪耳机', amount: 899, status: 'paid', createdAt: '2025-03-02', remark: '', createdBy: 'admin', updatedAt: '2025-03-03', items: [{ key: 'i1', productName: '降噪耳机', quantity: 1 }] },
  { id: 'o06', orderNo: 'NO-20250321-006', customer: '杨洋', product: '移动电源', amount: 159, status: 'shipped', createdAt: '2025-03-21', remark: '白色', createdBy: 'admin', updatedAt: '2025-03-24', items: [{ key: 'i1', productName: '移动电源', quantity: 1 }] },
  { id: 'o07', orderNo: 'NO-20250411-007', customer: '赵磊', product: '笔记本内胆包', amount: 199, status: 'pending', createdAt: '2025-04-11', remark: '', createdBy: 'admin', updatedAt: '2025-04-11', items: [{ key: 'i1', productName: '14 寸内胆包', quantity: 1 }, { key: 'i2', productName: '16 寸内胆包', quantity: 1 }] },
  { id: 'o08', orderNo: 'NO-20250425-008', customer: '黄丽', product: '桌面收纳盒', amount: 49.9, status: 'paid', createdAt: '2025-04-25', remark: '三个装', createdBy: 'admin', updatedAt: '2025-04-26', items: [{ key: 'i1', productName: '桌面收纳盒', quantity: 3 }] },
  { id: 'o09', orderNo: 'NO-20250506-009', customer: '周涛', product: 'HDMI 线缆', amount: 39, status: 'shipped', createdAt: '2025-05-06', remark: '2 米', createdBy: 'admin', updatedAt: '2025-05-08', items: [{ key: 'i1', productName: 'HDMI 线缆 2m', quantity: 2 }] },
  { id: 'o10', orderNo: 'NO-20250518-010', customer: '吴芳', product: '台灯', amount: 219, status: 'cancelled', createdAt: '2025-05-18', remark: '缺货取消', createdBy: 'admin', updatedAt: '2025-05-19', items: [{ key: 'i1', productName: '护眼台灯', quantity: 1 }] },
  { id: 'o11', orderNo: 'NO-20250604-011', customer: '徐明', product: '路由器', amount: 499, status: 'paid', createdAt: '2025-06-04', remark: '', createdBy: 'admin', updatedAt: '2025-06-05', items: [{ key: 'i1', productName: '双频路由器', quantity: 1 }] },
  { id: 'o12', orderNo: 'NO-20250622-012', customer: '孙丽', product: '摄像头', amount: 329, status: 'pending', createdAt: '2025-06-22', remark: '需上门安装', createdBy: 'admin', updatedAt: '2025-06-22', items: [{ key: 'i1', productName: '高清摄像头', quantity: 1 }] },
]

/** 模块级单例数据源（纯前端会话内共享，跨列表/表单/详情路由保持；非 Pinia） */
const orders = ref<OrderRow[]>(cloneRows(SEED))

/**
 * 订单数据源（模块级单例）
 * 供统一列表（/form）的列表、抽屉/页面表单、统一详情页面共享会话内数据
 */
export function useOrderData() {
  /** 生成订单号：NO-<时间戳后 6 位>-<随机 2 位> */
  function genOrderNo(): string {
    const ts = String(Date.now()).slice(-6)
    const rand = String(Math.floor(Math.random() * 100)).padStart(2, '0')
    return `NO-${ts}-${rand}`
  }

  function findById(id: string): OrderRow | undefined {
    return orders.value.find((o) => o.id === id)
  }

  /** 新增或更新（按 id 覆盖）；新增时自动生成 id 与订单号、设置创建人/时间 */
  function upsert(row: OrderRow): OrderRow {
    const today = new Date().toISOString().slice(0, 10)
    const idx = orders.value.findIndex((o) => o.id === row.id)
    const next: OrderRow = {
      ...row,
      orderNo: row.orderNo || genOrderNo(),
      createdBy: row.createdBy || 'admin',
      createdAt: row.createdAt || today,
      updatedAt: today,
      items: row.items.map((i) => ({ ...i })),
    }
    if (idx >= 0) {
      orders.value[idx] = next
    } else {
      next.id = `o${Date.now()}`
      orders.value.unshift(next)
    }
    return next
  }

  function remove(id: string): void {
    orders.value = orders.value.filter((o) => o.id !== id)
  }

  /** 恢复初始种子数据（刷新演示用） */
  function reset(): void {
    orders.value = cloneRows(SEED)
  }

  return { orders, genOrderNo, findById, upsert, remove, reset }
}
