import { expect, type Locator, type Page } from '@playwright/test'

/**
 * Arco Message 提示内容（最新一条）。
 *
 * Arco Message 同文案在展示期内（默认 3s）连续弹出会堆叠：旧节点尚未销毁，
 * 新的同文案节点已插入，此时 `getByText('xx已创建')` 会命中多个元素并触发
 * strict mode violation（表现为「连续创建两个同类实体」的用例偶发失败）。
 * 故统一用本定位器取最新一条，避免多元素冲突。
 */
export function messageLocator(page: Page, text: string): Locator {
  return page.locator('.arco-message-content', { hasText: text }).last()
}

/** 断言某条 Message 提示可见（同文案堆叠时取最新一条，见 messageLocator） */
export async function expectMessage(page: Page, text: string): Promise<void> {
  await expect(messageLocator(page, text)).toBeVisible()
}
