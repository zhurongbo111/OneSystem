<script setup lang="ts">
import { ref, type Component, type FunctionalComponent } from 'vue'

import { Message, Notification } from '@arco-design/web-vue'
import type { TableColumnData, TreeNodeData, CascaderOption } from '@arco-design/web-vue'
import {
  IconCalendar as ArcoIconCalendar,
  IconCloud as ArcoIconCloud,
  IconFolder as ArcoIconFolder,
  IconHeart as ArcoIconHeart,
  IconHome as ArcoIconHome,
  IconLock,
  IconMessage,
  IconNotification,
  IconPlayCircle,
  IconPlus as ArcoIconPlus,
  IconSearch as ArcoIconSearch,
  IconSettings as ArcoIconSettings,
  IconStar as ArcoIconStar,
  IconSync,
  IconUser as ArcoIconUser,
} from '@arco-design/web-vue/es/icon'
import {
  Bell,
  Calendar,
  Cloud,
  Folder,
  Heart,
  House,
  Mail,
  Plus,
  Search,
  Settings,
  Shield,
  Star,
  User,
} from '@lucide/vue'
import {
  IconBell,
  IconCalendar,
  IconCloud,
  IconFolder,
  IconHeart,
  IconHome,
  IconMail,
  IconPlus,
  IconSearch,
  IconSettings,
  IconShield,
  IconStar,
  IconUser,
} from '@tabler/icons-vue'

// —— constants ——
const selectOptions = [
  { label: '前端', value: 'fe' },
  { label: '后端', value: 'be' },
  { label: '测试', value: 'qa' },
]
const checkOptions = [
  { label: '选项一', value: 'a' },
  { label: '选项二', value: 'b' },
  { label: '选项三', value: 'c' },
]
const cascadeOptions: CascaderOption[] = [
  {
    value: 'zhejiang',
    label: '浙江省',
    children: [{ value: 'hangzhou', label: '杭州市' }, { value: 'ningbo', label: '宁波市' }],
  },
  { value: 'jiangsu', label: '江苏省' },
]
const tableColumns: TableColumnData[] = [
  { title: '姓名', dataIndex: 'name' },
  { title: '岗位', dataIndex: 'role' },
  { title: '状态', dataIndex: 'status' },
]
const tableData = [
  { name: '张三', role: '前端', status: '启用' },
  { name: '李四', role: '后端', status: '禁用' },
  { name: '王五', role: '测试', status: '启用' },
]
const treeData: TreeNodeData[] = [
  {
    key: '1',
    title: '父节点 1',
    children: [{ key: '1-1', title: '子节点 1-1' }, { key: '1-2', title: '子节点 1-2' }],
  },
  { key: '2', title: '父节点 2' },
]
// Tabler 图标示例数据（@tabler/icons-vue）
const tablerCommonIcons: { name: string; icon: FunctionalComponent }[] = [
  { name: 'IconHome', icon: IconHome },
  { name: 'IconSearch', icon: IconSearch },
  { name: 'IconUser', icon: IconUser },
  { name: 'IconBell', icon: IconBell },
  { name: 'IconSettings', icon: IconSettings },
  { name: 'IconCalendar', icon: IconCalendar },
  { name: 'IconHeart', icon: IconHeart },
  { name: 'IconStar', icon: IconStar },
  { name: 'IconMail', icon: IconMail },
  { name: 'IconFolder', icon: IconFolder },
  { name: 'IconShield', icon: IconShield },
  { name: 'IconCloud', icon: IconCloud },
]
// Arco 图标示例数据（@arco-design/web-vue 自带 Icon*）
const arcoCommonIcons: { name: string; icon: Component }[] = [
  { name: 'IconHome', icon: ArcoIconHome },
  { name: 'IconSearch', icon: ArcoIconSearch },
  { name: 'IconUser', icon: ArcoIconUser },
  { name: 'IconSettings', icon: ArcoIconSettings },
  { name: 'IconCalendar', icon: ArcoIconCalendar },
  { name: 'IconHeart', icon: ArcoIconHeart },
  { name: 'IconStar', icon: ArcoIconStar },
  { name: 'IconNotification', icon: IconNotification },
  { name: 'IconFolder', icon: ArcoIconFolder },
  { name: 'IconCloud', icon: ArcoIconCloud },
  { name: 'IconMessage', icon: IconMessage },
  { name: 'IconLock', icon: IconLock },
]
const arcoStrokeWidths = [1, 2, 3, 4]
const arcoRotates = [45, 90, 180]
// 三套图标共用的尺寸 / 颜色示例数据
const iconSizes = [16, 24, 32, 48]
const iconColors = [
  { name: '#165DFF', color: '#165DFF' },
  { name: '#00B42A', color: '#00B42A' },
  { name: '#F53F3F', color: '#F53F3F' },
  { name: '#FF7D00', color: '#FF7D00' },
]
const tablerStrokeWidths = ['1', '2', '3']
// Lucide 图标示例数据（@lucide/vue）
const lucideCommonIcons: { name: string; icon: FunctionalComponent }[] = [
  { name: 'House', icon: House },
  { name: 'Search', icon: Search },
  { name: 'User', icon: User },
  { name: 'Bell', icon: Bell },
  { name: 'Settings', icon: Settings },
  { name: 'Calendar', icon: Calendar },
  { name: 'Heart', icon: Heart },
  { name: 'Star', icon: Star },
  { name: 'Mail', icon: Mail },
  { name: 'Folder', icon: Folder },
  { name: 'Shield', icon: Shield },
  { name: 'Cloud', icon: Cloud },
]
const lucideStrokeWidths = [1, 2, 3]

// —— reactive state ——
// 当前激活的分类 tab
const activeTab = ref('base')
const inputValue = ref('')
const selectValue = ref<string | undefined>(undefined)
const checkValues = ref<string[]>([])
const radioValue = ref<string>('1')
const switchValue = ref(true)
const sliderValue = ref(30)
const rateValue = ref(3)
const dateValue = ref<Date | string | undefined>(undefined)
const cascadeValue = ref<string[]>([])
const modalVisible = ref(false)
const drawerVisible = ref(false)

// —— methods ——
function showMessage(): void {
  Message.success('这是一条 Message 提示')
}
function showNotification(): void {
  Notification.info({ title: '提示', content: '这是一条 Notification 通知' })
}
</script>

<template>
  <div class="showcase-page">
    <header class="showcase-header">
      <h1 class="showcase-title">
        Arco Design 组件示例
      </h1>
    </header>

    <a-tabs
      v-model:active-key="activeTab"
      class="showcase-tabs"
    >
      <!-- 基础 -->
      <a-tab-pane
        key="base"
        title="基础"
      >
        <a-row :gutter="16">
          <a-col :span="12">
            <a-card
              title="按钮 Button"
              :bordered="false"
            >
              <a-space>
                <a-button type="primary">
                  主要按钮
                </a-button>
                <a-button>默认按钮</a-button>
                <a-button type="outline">
                  描边按钮
                </a-button>
                <a-button type="dashed">
                  虚线按钮
                </a-button>
                <a-button type="text">
                  文本按钮
                </a-button>
                <a-button
                  type="primary"
                  status="danger"
                >
                  危险按钮
                </a-button>
              </a-space>
            </a-card>
          </a-col>
          <a-col :span="12">
            <a-card
              title="文字排版 Typography"
              :bordered="false"
            >
              <a-space
                direction="vertical"
                fill
              >
                <a-typography-title :heading="6">
                  六级标题
                </a-typography-title>
                <a-typography-paragraph>这是一段示例段落文字，用于展示 Typography 段落效果。</a-typography-paragraph>
                <a-typography-text type="secondary">
                  次级文本
                </a-typography-text>
                <a-typography-link>这是一个链接</a-typography-link>
                <a-typography-text>
                  内联文本：<a-typography-text type="warning">
                    警告
                  </a-typography-text>
                  <a-typography-text type="danger">
                    危险
                  </a-typography-text>
                </a-typography-text>
              </a-space>
            </a-card>
          </a-col>
        </a-row>
      </a-tab-pane>

      <!-- 表单 -->
      <a-tab-pane
        key="form"
        title="表单"
      >
        <a-row :gutter="16">
          <a-col :span="12">
            <a-card
              title="输入框 Input"
              :bordered="false"
            >
              <a-space
                direction="vertical"
                fill
              >
                <a-input
                  v-model="inputValue"
                  placeholder="请输入内容"
                  allow-clear
                />
                <a-input placeholder="带前缀">
                  <template #prefix>
                    <span class="prefix-icon">@</span>
                  </template>
                </a-input>
              </a-space>
            </a-card>
          </a-col>
          <a-col :span="12">
            <a-card
              title="选择器 Select"
              :bordered="false"
            >
              <a-select
                v-model="selectValue"
                :options="selectOptions"
                placeholder="请选择岗位"
                allow-clear
                style="width: 100%"
              />
              <div class="value-preview">
                当前值：{{ selectValue ?? '（空）' }}
              </div>
            </a-card>
          </a-col>

          <a-col :span="12">
            <a-card
              title="复选框 Checkbox"
              :bordered="false"
            >
              <a-checkbox-group
                v-model="checkValues"
                :options="checkOptions"
              />
            </a-card>
          </a-col>
          <a-col :span="12">
            <a-card
              title="单选框 Radio"
              :bordered="false"
            >
              <a-radio-group v-model="radioValue">
                <a-radio value="1">
                  选项一
                </a-radio>
                <a-radio value="2">
                  选项二
                </a-radio>
                <a-radio value="3">
                  选项三
                </a-radio>
              </a-radio-group>
            </a-card>
          </a-col>

          <a-col :span="12">
            <a-card
              title="开关 Switch / 评分 Rate"
              :bordered="false"
            >
              <a-space
                direction="vertical"
                fill
              >
                <a-space align="center">
                  <a-switch v-model="switchValue" />
                  <span>开关：{{ switchValue ? '开' : '关' }}</span>
                </a-space>
                <a-rate v-model="rateValue" />
              </a-space>
            </a-card>
          </a-col>
          <a-col :span="12">
            <a-card
              title="滑块 Slider"
              :bordered="false"
            >
              <a-slider
                v-model="sliderValue"
                :min="0"
                :max="100"
              />
              <div class="value-preview">
                当前值：{{ sliderValue }}
              </div>
            </a-card>
          </a-col>

          <a-col :span="12">
            <a-card
              title="日期选择 DatePicker"
              :bordered="false"
            >
              <a-date-picker
                v-model="dateValue"
                style="width: 100%"
              />
            </a-card>
          </a-col>
          <a-col :span="12">
            <a-card
              title="级联选择 Cascader"
              :bordered="false"
            >
              <a-cascader
                v-model="cascadeValue"
                :options="cascadeOptions"
                placeholder="请选择地区"
                allow-clear
                style="width: 100%"
              />
            </a-card>
          </a-col>

          <a-col :span="24">
            <a-card
              title="上传 Upload"
              :bordered="false"
            >
              <a-upload
                :auto-upload="false"
                drag
              >
                <div class="upload-content">
                  <p class="upload-title">
                    点击或拖拽文件到此区域上传
                  </p>
                  <p class="upload-sub">
                    支持任意文件，仅前端展示，不实际上传
                  </p>
                </div>
              </a-upload>
            </a-card>
          </a-col>
        </a-row>
      </a-tab-pane>

      <!-- 数据展示 -->
      <a-tab-pane
        key="data"
        title="数据展示"
      >
        <a-row :gutter="16">
          <a-col :span="24">
            <a-card
              title="表格 Table"
              :bordered="false"
            >
              <a-table
                :columns="tableColumns"
                :data="tableData"
                :pagination="{ pageSize: 10 }"
                row-key="name"
              />
            </a-card>
          </a-col>

          <a-col :span="8">
            <a-card
              title="标签 Tag"
              :bordered="false"
            >
              <a-space wrap>
                <a-tag color="arcoblue">
                  蓝色
                </a-tag>
                <a-tag color="green">
                  绿色
                </a-tag>
                <a-tag color="orangered">
                  橙色
                </a-tag>
                <a-tag
                  color="red"
                  closable
                >
                  可关闭
                </a-tag>
              </a-space>
            </a-card>
          </a-col>
          <a-col :span="8">
            <a-card
              title="徽标 Badge"
              :bordered="false"
            >
              <a-space size="large">
                <a-badge :count="5" />
                <a-badge
                  :count="10"
                  :max-count="9"
                />
                <a-badge dot>
                  <a-avatar>U</a-avatar>
                </a-badge>
              </a-space>
            </a-card>
          </a-col>
          <a-col :span="8">
            <a-card
              title="头像 Avatar"
              :bordered="false"
            >
              <a-space>
                <a-avatar>李</a-avatar>
                <a-avatar shape="square">
                  方
                </a-avatar>
                <a-avatar-group :max-count="3">
                  <a-avatar>A</a-avatar>
                  <a-avatar>B</a-avatar>
                  <a-avatar>C</a-avatar>
                  <a-avatar>D</a-avatar>
                </a-avatar-group>
              </a-space>
            </a-card>
          </a-col>

          <a-col :span="12">
            <a-card
              title="描述列表 Descriptions"
              :bordered="false"
            >
              <a-descriptions :column="1">
                <a-descriptions-item label="产品">
                  Arco Design
                </a-descriptions-item>
                <a-descriptions-item label="状态">
                  <a-tag color="green">
                    运行中
                  </a-tag>
                </a-descriptions-item>
                <a-descriptions-item label="更新时间">
                  2026-09-09
                </a-descriptions-item>
              </a-descriptions>
            </a-card>
          </a-col>
          <a-col :span="12">
            <a-card
              title="时间线 Timeline"
              :bordered="false"
            >
              <a-timeline>
                <a-timeline-item dot="success">
                  已完成
                </a-timeline-item>
                <a-timeline-item dot="arcoblue">
                  进行中
                </a-timeline-item>
                <a-timeline-item>待处理</a-timeline-item>
              </a-timeline>
            </a-card>
          </a-col>

          <a-col :span="8">
            <a-card
              title="统计数值 Statistic"
              :bordered="false"
            >
              <a-statistic
                title="访问量"
                :value="112833"
              />
            </a-card>
          </a-col>
          <a-col :span="8">
            <a-card
              title="进度条 Progress"
              :bordered="false"
            >
              <a-progress :percent="60" />
            </a-card>
          </a-col>
          <a-col :span="8">
            <a-card
              title="树形控件 Tree"
              :bordered="false"
            >
              <a-tree :data="treeData" />
            </a-card>
          </a-col>
        </a-row>
      </a-tab-pane>

      <!-- 反馈 -->
      <a-tab-pane
        key="feedback"
        title="反馈"
      >
        <a-row :gutter="16">
          <a-col :span="24">
            <a-card
              title="警告提示 Alert"
              :bordered="false"
            >
              <a-space
                direction="vertical"
                fill
              >
                <a-alert
                  type="success"
                  title="成功提示"
                >
                  这是一条成功类型的提示
                </a-alert>
                <a-alert
                  type="warning"
                  title="警告提示"
                >
                  这是一条警告类型的提示
                </a-alert>
                <a-alert
                  type="error"
                  title="错误提示"
                >
                  这是一条错误类型的提示
                </a-alert>
              </a-space>
            </a-card>
          </a-col>

          <a-col :span="12">
            <a-card
              title="结果 Result"
              :bordered="false"
            >
              <a-result
                status="success"
                title="操作成功"
                sub-title="这是一条成功结果页的说明文字。"
              />
            </a-card>
          </a-col>
          <a-col :span="12">
            <a-card
              title="加载 Spin / 骨架 Skeleton"
              :bordered="false"
            >
              <a-space
                direction="vertical"
                fill
              >
                <a-space align="center">
                  <a-spin :loading="true" />
                  <span>加载中...</span>
                </a-space>
                <a-skeleton :animation="true">
                  <template #template>
                    <a-row :gutter="12">
                      <a-col :span="4">
                        <a-skeleton-avatar />
                      </a-col>
                      <a-col :span="20">
                        <a-skeleton-paragraph :line-count="3" />
                      </a-col>
                    </a-row>
                  </template>
                </a-skeleton>
              </a-space>
            </a-card>
          </a-col>

          <a-col :span="12">
            <a-card
              title="弹框 Modal / 抽屉 Drawer"
              :bordered="false"
            >
              <a-space>
                <a-button
                  type="primary"
                  @click="modalVisible = true"
                >
                  打开弹窗
                </a-button>
                <a-button @click="drawerVisible = true">
                  打开抽屉
                </a-button>
              </a-space>
            </a-card>
          </a-col>
          <a-col :span="12">
            <a-card
              title="气泡提示 Tooltip / Popover"
              :bordered="false"
            >
              <a-space>
                <a-tooltip content="这是 Tooltip 提示">
                  <a-button>悬停我（Tooltip）</a-button>
                </a-tooltip>
                <a-popover
                  position="top"
                  title="标题"
                >
                  <template #content>
                    这是 Popover 的详细内容区域
                  </template>
                  <a-button>悬停我（Popover）</a-button>
                </a-popover>
              </a-space>
            </a-card>
          </a-col>

          <a-col :span="24">
            <a-card
              title="通知 Notification / 全局提示 Message"
              :bordered="false"
            >
              <a-space>
                <a-button
                  type="primary"
                  @click="showNotification"
                >
                  打开通知
                </a-button>
                <a-button @click="showMessage">
                  全局提示
                </a-button>
              </a-space>
            </a-card>
          </a-col>

          <a-modal
            v-model:visible="modalVisible"
            title="弹窗示例"
            ok-text="确定"
            unmount-on-close
          >
            <p>这是一个 Arco Modal 弹框的示例内容。</p>
          </a-modal>
          <a-drawer
            v-model:visible="drawerVisible"
            title="抽屉示例"
            :width="380"
            ok-text="确定"
            unmount-on-close
          >
            <p>这是一个 Arco Drawer 抽屉的示例内容。</p>
          </a-drawer>
        </a-row>
      </a-tab-pane>

      <!-- 导航 -->
      <a-tab-pane
        key="nav"
        title="导航"
      >
        <a-row :gutter="16">
          <a-col :span="8">
            <a-card
              title="菜单 Menu"
              :bordered="false"
            >
              <a-menu
                :width="200"
                mode="inline"
              >
                <a-menu-item key="1">
                  首页
                </a-menu-item>
                <a-menu-item key="2">
                  组件
                </a-menu-item>
                <a-menu-item key="3">
                  文档
                </a-menu-item>
                <a-menu-item
                  key="4"
                  disabled
                >
                  禁用项
                </a-menu-item>
              </a-menu>
            </a-card>
          </a-col>
          <a-col :span="16">
            <a-card
              title="嵌套 Tabs / 面包屑 Breadcrumb"
              :bordered="false"
            >
              <a-space
                direction="vertical"
                fill
              >
                <a-breadcrumb>
                  <a-breadcrumb-item>首页</a-breadcrumb-item>
                  <a-breadcrumb-item>组件</a-breadcrumb-item>
                  <a-breadcrumb-item>导航</a-breadcrumb-item>
                </a-breadcrumb>
                <a-tabs
                  size="small"
                  default-active-key="1"
                >
                  <a-tab-pane
                    key="1"
                    title="标签一"
                  >
                    标签一的内容
                  </a-tab-pane>
                  <a-tab-pane
                    key="2"
                    title="标签二"
                  >
                    标签二的内容
                  </a-tab-pane>
                </a-tabs>
              </a-space>
            </a-card>
          </a-col>

          <a-col :span="12">
            <a-card
              title="步骤条 Steps"
              :bordered="false"
            >
              <a-steps :current="2">
                <a-step description="第一步说明">
                  第一步
                </a-step>
                <a-step description="第二步说明">
                  第二步
                </a-step>
                <a-step description="第三步说明">
                  第三步
                </a-step>
              </a-steps>
            </a-card>
          </a-col>
          <a-col :span="12">
            <a-card
              title="分页 Pagination"
              :bordered="false"
            >
              <a-pagination
                :total="100"
                :page-size="10"
                show-total
              />
            </a-card>
          </a-col>

          <a-col :span="24">
            <a-card
              title="下拉菜单 Dropdown"
              :bordered="false"
            >
              <a-dropdown>
                <a-button>
                  操作菜单
                  <template #icon>
                    <icon-down />
                  </template>
                </a-button>
                <template #content>
                  <a-doption>新建</a-doption>
                  <a-doption>编辑</a-doption>
                  <a-doption>删除</a-doption>
                </template>
              </a-dropdown>
            </a-card>
          </a-col>
        </a-row>
      </a-tab-pane>

      <!-- 图标 -->
      <a-tab-pane
        key="icon"
        title="图标"
      >
        <!-- Arco 段（@arco-design/web-vue 自带 Icon*，业务默认使用） -->
        <a-row :gutter="16">
          <a-col :span="24">
            <a-card
              title="Arco 常用图标"
              :bordered="false"
            >
              <div class="icon-grid">
                <div
                  v-for="item in arcoCommonIcons"
                  :key="item.name"
                  class="icon-grid-item"
                >
                  <component
                    :is="item.icon"
                    size="24"
                  />
                  <span class="icon-name">
                    {{ item.name }}
                  </span>
                </div>
              </div>
            </a-card>
          </a-col>

          <a-col :span="8">
            <a-card
              title="尺寸 size"
              :bordered="false"
            >
              <a-space align="center">
                <ArcoIconStar
                  v-for="size in iconSizes"
                  :key="size"
                  :size="size"
                />
              </a-space>
              <div class="value-preview">
                16 / 24 / 32 / 48
              </div>
            </a-card>
          </a-col>
          <a-col :span="8">
            <a-card
              title="线宽 stroke-width"
              :bordered="false"
            >
              <a-space align="center">
                <ArcoIconStar
                  v-for="width in arcoStrokeWidths"
                  :key="width"
                  :size="24"
                  :stroke-width="width"
                />
              </a-space>
              <div class="value-preview">
                1 / 2 / 3 / 4（默认 4）
              </div>
            </a-card>
          </a-col>
          <a-col :span="8">
            <a-card
              title="颜色 color（CSS color）"
              :bordered="false"
            >
              <a-space align="center">
                <ArcoIconStar
                  v-for="item in iconColors"
                  :key="item.name"
                  :size="24"
                  :style="{ color: item.color }"
                />
              </a-space>
              <div class="value-preview">
                #165DFF / #00B42A / #F53F3F / #FF7D00
              </div>
            </a-card>
          </a-col>

          <a-col :span="12">
            <a-card
              title="旋转 rotate / 动画 spin（Arco 特有）"
              :bordered="false"
            >
              <a-space
                direction="vertical"
                fill
              >
                <a-space align="center">
                  <IconPlayCircle
                    v-for="angle in arcoRotates"
                    :key="angle"
                    :size="24"
                    :rotate="angle"
                  />
                </a-space>
                <a-space align="center">
                  <IconSync
                    :size="24"
                    spin
                  />
                  <span class="value-preview">
                    IconSync :spin=&quot;true&quot;
                  </span>
                </a-space>
              </a-space>
            </a-card>
          </a-col>
          <a-col :span="12">
            <a-card
              title="按钮 / 标签 / 菜单内嵌图标"
              :bordered="false"
            >
              <a-space
                direction="vertical"
                fill
              >
                <a-space>
                  <a-button type="primary">
                    <template #icon>
                      <ArcoIconPlus size="16" />
                    </template>
                    新建
                  </a-button>
                  <a-button>
                    <template #icon>
                      <ArcoIconSearch size="16" />
                    </template>
                    搜索
                  </a-button>
                  <a-button>
                    <template #icon>
                      <ArcoIconSettings size="16" />
                    </template>
                    设置
                  </a-button>
                </a-space>
                <a-space>
                  <a-tag color="arcoblue">
                    <template #icon>
                      <ArcoIconHome size="14" />
                    </template>
                    首页
                  </a-tag>
                  <a-tag color="green">
                    <template #icon>
                      <IconLock size="14" />
                    </template>
                    安全
                  </a-tag>
                  <a-tag color="orangered">
                    <template #icon>
                      <ArcoIconFolder size="14" />
                    </template>
                    文件
                  </a-tag>
                </a-space>
                <a-menu
                  :width="180"
                  mode="inline"
                >
                  <a-menu-item key="1">
                    <template #icon>
                      <IconNotification size="16" />
                    </template>
                    通知中心
                  </a-menu-item>
                  <a-menu-item key="2">
                    <template #icon>
                      <IconMessage size="16" />
                    </template>
                    我的消息
                  </a-menu-item>
                </a-menu>
              </a-space>
            </a-card>
          </a-col>
        </a-row>

        <div class="icon-lib-divider">
          Tabler Icons（@tabler/icons-vue）
        </div>
        <a-row :gutter="16">
          <a-col :span="24">
            <a-card
              title="Tabler 常用图标"
              :bordered="false"
            >
              <div class="icon-grid">
                <div
                  v-for="item in tablerCommonIcons"
                  :key="item.name"
                  class="icon-grid-item"
                >
                  <component
                    :is="item.icon"
                    size="24"
                  />
                  <span class="icon-name">
                    {{ item.name }}
                  </span>
                </div>
              </div>
            </a-card>
          </a-col>

          <a-col :span="8">
            <a-card
              title="尺寸 size"
              :bordered="false"
            >
              <a-space align="center">
                <IconStar
                  v-for="size in iconSizes"
                  :key="size"
                  :size="size"
                />
              </a-space>
              <div class="value-preview">
                16 / 24 / 32 / 48
              </div>
            </a-card>
          </a-col>
          <a-col :span="8">
            <a-card
              title="线宽 stroke"
              :bordered="false"
            >
              <a-space align="center">
                <IconStar
                  v-for="width in tablerStrokeWidths"
                  :key="width"
                  :size="24"
                  :stroke="width"
                />
              </a-space>
              <div class="value-preview">
                1 / 2 / 3
              </div>
            </a-card>
          </a-col>
          <a-col :span="8">
            <a-card
              title="颜色 color"
              :bordered="false"
            >
              <a-space align="center">
                <IconStar
                  v-for="item in iconColors"
                  :key="item.name"
                  :size="24"
                  :color="item.color"
                />
              </a-space>
              <div class="value-preview">
                #165DFF / #00B42A / #F53F3F / #FF7D00
              </div>
            </a-card>
          </a-col>

          <a-col :span="12">
            <a-card
              title="按钮内嵌图标"
              :bordered="false"
            >
              <a-space>
                <a-button type="primary">
                  <template #icon>
                    <IconPlus size="16" />
                  </template>
                  新建
                </a-button>
                <a-button>
                  <template #icon>
                    <IconSearch size="16" />
                  </template>
                  搜索
                </a-button>
                <a-button>
                  <template #icon>
                    <IconSettings size="16" />
                  </template>
                  设置
                </a-button>
              </a-space>
            </a-card>
          </a-col>
          <a-col :span="12">
            <a-card
              title="标签 / 菜单内嵌图标"
              :bordered="false"
            >
              <a-space
                direction="vertical"
                fill
              >
                <a-space>
                  <a-tag color="arcoblue">
                    <template #icon>
                      <IconHome size="14" />
                    </template>
                    首页
                  </a-tag>
                  <a-tag color="green">
                    <template #icon>
                      <IconShield size="14" />
                    </template>
                    安全
                  </a-tag>
                  <a-tag color="orangered">
                    <template #icon>
                      <IconFolder size="14" />
                    </template>
                    文件
                  </a-tag>
                </a-space>
                <a-menu
                  :width="180"
                  mode="inline"
                >
                  <a-menu-item key="1">
                    <template #icon>
                      <IconBell size="16" />
                    </template>
                    通知中心
                  </a-menu-item>
                  <a-menu-item key="2">
                    <template #icon>
                      <IconMail size="16" />
                    </template>
                    我的消息
                  </a-menu-item>
                </a-menu>
              </a-space>
            </a-card>
          </a-col>
        </a-row>

        <div class="icon-lib-divider">
          Lucide Icons（@lucide/vue）
        </div>
        <a-row :gutter="16">
          <a-col :span="24">
            <a-card
              title="Lucide 常用图标"
              :bordered="false"
            >
              <div class="icon-grid">
                <div
                  v-for="item in lucideCommonIcons"
                  :key="item.name"
                  class="icon-grid-item"
                >
                  <component
                    :is="item.icon"
                    :size="24"
                  />
                  <span class="icon-name">
                    {{ item.name }}
                  </span>
                </div>
              </div>
            </a-card>
          </a-col>

          <a-col :span="12">
            <a-card
              title="尺寸 size"
              :bordered="false"
            >
              <a-space align="center">
                <Star
                  v-for="size in iconSizes"
                  :key="size"
                  :size="size"
                />
              </a-space>
              <div class="value-preview">
                16 / 24 / 32 / 48
              </div>
            </a-card>
          </a-col>
          <a-col :span="12">
            <a-card
              title="线宽 stroke-width"
              :bordered="false"
            >
              <a-space align="center">
                <Star
                  v-for="width in lucideStrokeWidths"
                  :key="width"
                  :size="24"
                  :stroke-width="width"
                />
              </a-space>
              <div class="value-preview">
                1 / 2 / 3
              </div>
            </a-card>
          </a-col>
          <a-col :span="12">
            <a-card
              title="颜色 color"
              :bordered="false"
            >
              <a-space align="center">
                <Star
                  v-for="item in iconColors"
                  :key="item.name"
                  :size="24"
                  :color="item.color"
                />
              </a-space>
              <div class="value-preview">
                #165DFF / #00B42A / #F53F3F / #FF7D00
              </div>
            </a-card>
          </a-col>
          <a-col :span="12">
            <a-card
              title="按钮 / 标签内嵌图标"
              :bordered="false"
            >
              <a-space
                direction="vertical"
                fill
              >
                <a-space>
                  <a-button type="primary">
                    <template #icon>
                      <Plus :size="16" />
                    </template>
                    新建
                  </a-button>
                  <a-button>
                    <template #icon>
                      <Search :size="16" />
                    </template>
                    搜索
                  </a-button>
                  <a-button>
                    <template #icon>
                      <Settings :size="16" />
                    </template>
                    设置
                  </a-button>
                </a-space>
                <a-space>
                  <a-tag color="arcoblue">
                    <template #icon>
                      <House :size="14" />
                    </template>
                    首页
                  </a-tag>
                  <a-tag color="green">
                    <template #icon>
                      <Shield :size="14" />
                    </template>
                    安全
                  </a-tag>
                  <a-tag color="orangered">
                    <template #icon>
                      <Folder :size="14" />
                    </template>
                    文件
                  </a-tag>
                </a-space>
              </a-space>
            </a-card>
          </a-col>
        </a-row>
      </a-tab-pane>
    </a-tabs>
  </div>
</template>

<style scoped>
.showcase-page {
  min-height: 100%;
  padding: 24px;
  background: var(--color-fill-2);
}

.showcase-header {
  margin-bottom: 16px;
}

.showcase-title {
  margin: 0;
  font-size: 20px;
  font-weight: 600;
}

.showcase-tabs {
  background: transparent;
}

.value-preview {
  margin-top: 8px;
  color: var(--color-text-2);
  font-size: 13px;
}

.icon-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(96px, 1fr));
  gap: 12px;
}

.icon-grid-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
  padding: 12px 0;
  color: var(--color-text-1);
}

.icon-name {
  color: var(--color-text-3);
  font-size: 12px;
}

.icon-lib-divider {
  margin: 24px 0 16px;
  padding-top: 16px;
  border-top: 1px solid var(--color-border-2);
  color: var(--color-text-2);
  font-size: 14px;
  font-weight: 500;
}

.prefix-icon {
  color: var(--color-text-3);
}

.upload-content {
  padding: 24px 0;
  text-align: center;
}

.upload-title {
  margin: 0 0 4px;
  font-size: 15px;
}

.upload-sub {
  margin: 0;
  color: var(--color-text-3);
  font-size: 13px;
}
</style>
