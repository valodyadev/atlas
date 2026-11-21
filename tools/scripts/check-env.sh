#!/usr/bin/env bash
# Reports which toolchains are installed, and what each missing one blocks.
set -uo pipefail

ok=0
missing=0

check() {
  local name="$1" probe="$2" needed_for="$3" install_hint="$4"
  if eval "${probe}" >/dev/null 2>&1; then
    local version
    version="$(eval "${probe}" 2>/dev/null | head -n 1)"
    printf '  \033[32m✓\033[0m %-14s %s\n' "${name}" "${version}"
    ok=$((ok + 1))
  else
    printf '  \033[31m✗\033[0m %-14s missing - needed for %s\n' "${name}" "${needed_for}"
    printf '      install: %s\n' "${install_hint}"
    missing=$((missing + 1))
  fi
}

echo
echo "Atlas toolchain check"
echo

echo "Backend (.NET)"
# The Homebrew formula is keg-only; make the keg visible for this check too.
if [ -d /opt/homebrew/opt/dotnet@10/bin ]; then
  PATH="/opt/homebrew/opt/dotnet@10/bin:${PATH}"
fi
check "dotnet" "dotnet --version" "the API, migrations and backend tests" "brew install dotnet@10"

echo
echo "iOS"
check "swift" "swift --version" "building and testing the shared iOS packages" "install Xcode or the Command Line Tools"
check "xcodegen" "xcodegen --version" "generating ios/Atlas.xcodeproj" "brew install xcodegen"
check "swiftformat" "swiftformat --version" "iOS formatting checks" "brew install swiftformat"
check "swiftlint" "swiftlint version" "iOS linting (needs a full Xcode install)" "brew install swiftlint"
if ! xcodebuild -version >/dev/null 2>&1; then
  printf '  \033[33m!\033[0m %-14s only the Command Line Tools are active\n' "Xcode"
  printf '      Building the iOS APP and running simulator tests require full Xcode.\n'
  printf '      The shared packages in ios/Packages/AtlasKit build and test without it.\n'
fi

echo
echo "Services"
check "node" "node --version" "the Node worker" "brew install node (or use nvm/fnm)"
check "uv" "uv --version" "the Python worker" "brew install uv"
check "python3" "python3 --version" "scripts and the Python worker" "brew install python@3.13"

echo
echo "Infrastructure"
check "docker" "docker --version" "the local stack and backend integration tests" "install Docker Desktop, Colima or OrbStack"
check "kustomize" "kustomize version" "rendering Kubernetes manifests" "brew install kustomize"
check "git" "git --version" "everything" "install Xcode Command Line Tools"

echo
if [ "${missing}" -eq 0 ]; then
  echo "All ${ok} tools present."
else
  echo "${ok} present, ${missing} missing."
  echo "Nothing is mandatory: each missing tool only disables the targets listed above."
fi
echo
