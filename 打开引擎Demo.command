#!/bin/zsh

repo_dir="${0:A:h}"
cd "$repo_dir" || exit 1

if ! command -v npm >/dev/null 2>&1; then
  echo "未找到 npm。请先安装 Node.js，然后重新双击本文件。"
  read -r "?按回车键关闭窗口……"
  exit 1
fi

if [[ ! -d node_modules ]]; then
  echo "首次运行，正在安装项目依赖……"
  if ! npm install; then
    echo "依赖安装失败，请检查上面的错误信息。"
    read -r "?按回车键关闭窗口……"
    exit 1
  fi
fi

echo "正在启动 SRPG 引擎 Demo……"
echo "页面打开后请保留此窗口；按 Control-C 或关闭窗口即可停止服务。"
npm run demo

status=$?
if (( status != 0 && status != 130 )); then
  echo "启动失败，请检查上面的错误信息。"
  read -r "?按回车键关闭窗口……"
fi

exit $status
