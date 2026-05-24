# Proxy Forward

Windows SSH 反向代理转发管理工具。

## 功能

- 保存多组 SSH 反向转发配置。
- 支持密码登录和私钥登录。
- 密码使用 Windows DPAPI CurrentUser 加密保存。
- 支持多条配置同时运行。
- 关闭主窗口后最小化到系统托盘，转发继续运行。
- 托盘菜单支持显示窗口、启动全部、停止全部、退出。
- 可选 HTTP 代理 Basic 鉴权，避免同一台服务器上的其他用户直接复用远程端口。

## 代理鉴权

编辑配置时勾选 `Require HTTP proxy authentication`，填写 `Proxy auth user` 和 `Proxy auth password`。

启用后，服务器上的代理地址需要带用户名和密码：

```bash
export HTTP_PROXY=http://user:password@127.0.0.1:43897
export HTTPS_PROXY=http://user:password@127.0.0.1:43897
export ALL_PROXY=http://user:password@127.0.0.1:43897

export http_proxy="$HTTP_PROXY"
export https_proxy="$HTTPS_PROXY"
export all_proxy="$ALL_PROXY"
```

未带认证信息的请求会返回：

```text
407 Proxy Authentication Required
```

## 配置保存位置

```text
%APPDATA%\ProxyForward\configs.json
```

日志位置：

```text
%APPDATA%\ProxyForward\logs\app.log
```

## 开发运行

```powershell
dotnet run
```

## 发布

```powershell
dotnet publish -c Release -r win-x64 --self-contained false
```

发布后的 exe 在：

```text
bin\Release\net9.0-windows\win-x64\publish\ProxyForward.exe
```
