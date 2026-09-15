import { get, post, put, del } from './request'

/** 商品状态：1 启用 / 0 停用 */
export type ProductStatus = 0 | 1

/** 分页结果（对应后端 PagedResult<T>，AGENTS.md §4.3） */
export interface PagedResult<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

/** 商品出参（列表 / 详情 / 新增 / 编辑共用，对应后端 ProductDto） */
export interface Product {
  id: string
  code: string
  name: string
  categoryId: string
  categoryName: string
  unit: string
  purchasePrice: number
  salePrice: number
  safetyStock: number
  stockQuantity: number
  isBelowSafetyStock: boolean
  status: ProductStatus
  remark: string | null
  createdAt: string
  updatedAt: string
}

/** 开单商品选择项（仅启用商品，对应后端 ProductPickDto） */
export interface ProductPickItem {
  id: string
  code: string
  name: string
  unit: string
  purchasePrice: number
  salePrice: number
  stockQuantity: number
}

/** 商品分类（对应后端 CategoryDto） */
export interface Category {
  id: string
  name: string
  createdAt: string
}

/** 商品列表查询参数（对应后端 GetProductsRequest） */
export interface ProductListQuery {
  keyword?: string
  categoryId?: string
  status?: ProductStatus
  page: number
  pageSize: number
}

/** 新增商品入参（对应后端 CreateProductRequest） */
export interface CreateProductPayload {
  code: string
  name: string
  categoryId: string
  unit: string
  purchasePrice: number
  salePrice: number
  safetyStock: number
  remark?: string
}

/** 编辑商品入参（对应后端 UpdateProductRequest，编码不可改） */
export interface UpdateProductPayload {
  name: string
  categoryId: string
  unit: string
  purchasePrice: number
  salePrice: number
  safetyStock: number
  remark?: string
}

/** 分页查询商品（支持关键词 / 分类 / 状态筛选，含库存与低库存标记） */
export function getProducts(query: ProductListQuery): Promise<PagedResult<Product>> {
  return get<PagedResult<Product>>('/products', { params: query })
}

/** 查询商品详情 */
export function getProduct(id: string): Promise<Product> {
  return get<Product>(`/products/${id}`)
}

/** 新增商品（后端同步初始化库存行 Quantity = 0） */
export function createProduct(payload: CreateProductPayload): Promise<Product> {
  return post<Product>('/products', payload)
}

/** 编辑商品（编码不可改） */
export function updateProduct(id: string, payload: UpdateProductPayload): Promise<Product> {
  return put<Product>(`/products/${id}`, payload)
}

/** 启用 / 停用商品 */
export function updateProductStatus(id: string, status: ProductStatus): Promise<Product> {
  return put<Product>(`/products/${id}/status`, { status })
}

/** 开单商品选择（仅启用商品，供 erp-purchase / erp-sale 消费） */
export function getProductPickList(): Promise<ProductPickItem[]> {
  return get<ProductPickItem[]>('/products/pick')
}

/** 分类分页查询参数（对应后端 GetCategoriesPagedRequest） */
export interface CategoryListQuery {
  keyword?: string
  page: number
  pageSize: number
}

/** 查询全部分类（创建时间正序，下拉 / 筛选用） */
export function getCategories(): Promise<Category[]> {
  return get<Category[]>('/categories')
}

/** 分页查询分类（名称模糊搜索 + 分页，创建时间正序，分类管理页用） */
export function getCategoriesPaged(query: CategoryListQuery): Promise<PagedResult<Category>> {
  return get<PagedResult<Category>>('/categories/paged', { params: query })
}

/** 新增分类（名称唯一，大小写不敏感） */
export function createCategory(name: string): Promise<Category> {
  return post<Category>('/categories', { name })
}

/** 编辑分类（名称唯一，排除自身） */
export function updateCategory(id: string, name: string): Promise<Category> {
  return put<Category>(`/categories/${id}`, { name })
}

/** 删除分类（被商品引用时后端返回 40106） */
export function deleteCategory(id: string): Promise<null> {
  return del<null>(`/categories/${id}`)
}
