export DOTNET_ROOT=/workspace/shared/empire-toolchain/dotnet
export DOTNET_CLI_HOME=/workspace/shared/empire-toolchain/home
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export NUGET_PACKAGES=/workspace/shared/empire-toolchain/nuget
export XDG_DATA_HOME=/workspace/shared/empire-toolchain/xdg-data
export XDG_CONFIG_HOME=/workspace/shared/empire-toolchain/xdg-config
export XDG_CACHE_HOME=/workspace/shared/empire-toolchain/xdg-cache
export PATH="$DOTNET_ROOT:$PATH"
export GODOT_NET=/workspace/shared/empire-toolchain/godot/Godot_v4.6.3-stable_mono_linux_x86_64/Godot_v4.6.3-stable_mono_linux.x86_64
export HOME="$DOTNET_CLI_HOME"
export XDG_RUNTIME_DIR=/workspace/shared/empire-toolchain/runtime
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
mkdir -p "$HOME" "$XDG_RUNTIME_DIR"
