// 042-erp-approval：四类单据的「生效」共享组件（*Fulfillment）分布在各自功能域命名空间下，
// 既有大批用例需要构造它们，统一在此全局引用，避免逐文件重复 using。
global using App.Core.Features.PurchaseReceipts;
global using App.Core.Features.PurchaseReturns;
global using App.Core.Features.SalesReturns;
global using App.Core.Features.SalesShipments;
