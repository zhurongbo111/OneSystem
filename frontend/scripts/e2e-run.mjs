/**
 * e2e 一键运行入口（`npm run e2e:run`）：按平台分发到本目录下的等价实现。
 *
 * - Windows：`e2e-run.ps1`（Windows PowerShell，需能调用 Get-NetTCPConnection / taskkill 等）
 * - Linux / macOS：`e2e-run.sh`（bash）
 *
 * 参数原样透传（如 `npm run e2e:run -- -Spec e2e/login.spec.ts`），退出码原样返回
 * （0 = 用例全通过、1 = 有用例失败、2 = 脚本级错误）。编排约定见
 * `specs/002-frontend-e2e/design.md` §6。
 */
import { spawnSync } from "node:child_process";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const args = process.argv.slice(2);

const isWindows = process.platform === "win32";
const command = isWindows ? "powershell.exe" : "bash";
const script = join(scriptDir, isWindows ? "e2e-run.ps1" : "e2e-run.sh");
const commandArgs = isWindows
  ? ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script, ...args]
  : [script, ...args];

const result = spawnSync(command, commandArgs, { stdio: "inherit" });

if (result.error) {
  console.error(`错误：无法调用 ${command}（${result.error.message}）`);
  process.exit(2);
}

// 被信号终止时 status 为 null，按脚本级错误处理
process.exit(result.status ?? 2);
