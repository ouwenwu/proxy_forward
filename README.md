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

## 工作方式

未开启代理鉴权时：

```text
服务器 127.0.0.1:远程端口
  -> SSH 反向转发
  -> 本机真实代理 127.0.0.1:本机端口
```

开启代理鉴权时：

```text
服务器 127.0.0.1:远程端口
  -> SSH 反向转发
  -> Proxy Forward 内置 Basic Auth 代理
  -> 本机真实代理 127.0.0.1:本机端口
```

因此同一台服务器上的其他用户即使知道远程端口，如果没有用户名和密码，也不能直接使用你的代理。

## 代理鉴权

编辑配置时勾选：

```text
Require HTTP proxy authentication
```

然后填写：

```text
Proxy auth user
Proxy auth password
```

启用后，服务器上的代理地址必须带用户名和密码：

```bash
export HTTP_PROXY=http://user:password@127.0.0.1:43897
export HTTPS_PROXY=http://user:password@127.0.0.1:43897
export ALL_PROXY=http://user:password@127.0.0.1:43897

export http_proxy="$HTTP_PROXY"
export https_proxy="$HTTPS_PROXY"
export all_proxy="$ALL_PROXY"
```

测试：

```bash
curl -I -x http://user:password@127.0.0.1:43897 https://www.google.com
```

正常应看到类似：

```text
HTTP/1.1 200 Connection established
HTTP/2 200
```

如果未带认证信息，或用户名密码不正确，会返回：

```text
407 Proxy Authentication Required
```

## Codex 使用示例

建议同时设置大写和小写代理变量：

```bash
export HTTP_PROXY=http://user:password@127.0.0.1:43897
export HTTPS_PROXY=http://user:password@127.0.0.1:43897
export ALL_PROXY=http://user:password@127.0.0.1:43897
export NO_PROXY=localhost,127.0.0.1,::1

export http_proxy="$HTTP_PROXY"
export https_proxy="$HTTPS_PROXY"
export all_proxy="$ALL_PROXY"
export no_proxy="$NO_PROXY"

codex
```

如果没有开启代理鉴权，则去掉 `user:password@`：

```bash
export HTTP_PROXY=http://127.0.0.1:43897
```

## 常见问题

### 407 Proxy Authentication Required

说明已经连到了 Proxy Forward 的鉴权层，但请求没有提供正确的代理用户名和密码。

检查代理地址是否为：

```text
http://user:password@127.0.0.1:43897
```

### TLS protocol version

如果已经出现：

```text
HTTP/1.1 200 Connection established
```

但随后出现：

```text
tlsv1 alert protocol version
```

通常说明 CONNECT 隧道已建立，但后续 TLS 数据异常。当前版本已经修复过鉴权代理层多转发空行导致的 HTTPS CONNECT 问题；请使用最新发布版后重新启动转发配置。

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
