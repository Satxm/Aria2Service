# Aria2Service
Aria2 Service for Windows.

# 使用
下载[Aria2](https://aria2.github.io/)并且和Aria2Service.exe放置到同一文件夹，运行```install-service.bat```将Aria2Service注册到系统；运行```uninstall-service.bat```将Aria2Service从系统服务中卸载。

Aria2Service 以 ```Aria2Service.exe```所在文件夹为工作目录，故需要在同一文件夹下有 ```aria2c.exe``` ```aria2.conf```等 aria2 运行所需文件。

```install-service.bat``` ```uninstall-service.bat```需要以管理员身份运行，脚本会在脚本所在文件夹及脚本上级文件夹中寻找 ```Aria2Service.exe```。
