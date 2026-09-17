import { expect, type Locator, type Page } from '@playwright/test'

/** 侧边菜单项 → 所属子菜单分组（顶级菜单项不在本表内） */
const MENU_GROUP_MAP: Record<string, string> = {
  组件示例: '示例页面',
  列表示例: '示例页面',
  表单与详情示例: '示例页面',
  商品管理: '进销存',
  分类管理: '进销存',
  往来单位: '进销存',
  库存查询: '进销存',
  库存流水: '进销存',
  采购订单: '进销存',
  采购入库: '进销存',
  采购退货: '进销存',
  销售订单: '进销存',
  销售出库: '进销存',
  销售退货: '进销存',
  收付款: '进销存',
  往来对账: '进销存',
  库存盘点: '进销存',
}

/** 侧边菜单叶子项 */
export function menuItem(page: Page, name: string): Locator {
  return page.locator('.arco-menu-item', { hasText: name })
}

/** 侧边子菜单分组标题 */
export function menuGroup(page: Page, name: string): Locator {
  return page.locator('.arco-menu-inline-header', { hasText: name })
}

/**
 * 点击侧边菜单项：子菜单默认折叠，所属分组处于折叠态时先点分组标题展开
 * （Arco 折叠态下子项 `display: none`，直接 click 会等待可见超时）
 */
export async function clickMenuItem(page: Page, name: string): Promise<void> {
  const item = menuItem(page, name)
  const group = MENU_GROUP_MAP[name]
  if (group && !(await item.isVisible())) {
    await menuGroup(page, group).click()
  }
  await expect(item).toBeVisible()
  await item.click()
}
