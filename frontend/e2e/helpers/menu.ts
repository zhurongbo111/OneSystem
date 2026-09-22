import { expect, type Locator, type Page } from '@playwright/test'

/** 侧边菜单项 → 所属顶级分组（顶级菜单项不在本表内；分组规划见 specs/025-erp-report/design.md §0.2） */
const MENU_GROUP_MAP: Record<string, string> = {
  组件示例: '示例页面',
  列表示例: '示例页面',
  表单与详情示例: '示例页面',
  商品管理: '基础档案',
  分类管理: '基础档案',
  往来单位: '基础档案',
  采购订单: '采购',
  采购入库: '采购',
  采购退货: '采购',
  销售订单: '销售',
  销售出库: '销售',
  销售退货: '销售',
  库存查询: '库存',
  库存流水: '库存',
  库存盘点: '库存',
  收付款: '资金',
  往来对账: '资金',
  进销存报表: '报表',
  库存余额表: '报表',
  采购汇总: '报表',
  销售汇总: '报表',
  成本与毛利: '报表',
  用户管理: '系统',
  登录日志: '系统',
  操作日志: '系统',
  角色权限: '系统',
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
 * 确保菜单项处于展开可见态：子菜单默认折叠，折叠态子项 `display: none`
 * （直接断言可见会失败，需先点开所属分组；分组已在 MENU_GROUP_MAP 登记）
 */
export async function ensureMenuItemVisible(page: Page, name: string): Promise<void> {
  const item = menuItem(page, name)
  const group = MENU_GROUP_MAP[name]
  if (group && !(await item.isVisible())) {
    await menuGroup(page, group).click()
  }
  await expect(item).toBeVisible()
}

/** 点击侧边菜单项（先确保展开可见） */
export async function clickMenuItem(page: Page, name: string): Promise<void> {
  await ensureMenuItemVisible(page, name)
  await menuItem(page, name).click()
}
