#!/usr/bin/env bash
# Installs the TeknoParrot udev rule that lets the logged-in desktop user read
# input devices (light guns, mice, keyboards) without root or 'input' group.
#
# Usage:  sudo ./install-udev-rules.sh          install
#         sudo ./install-udev-rules.sh --remove uninstall
set -euo pipefail

RULE_NAME="70-teknoparrot-input.rules"
RULE_SRC="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/${RULE_NAME}"
RULE_DST="/etc/udev/rules.d/${RULE_NAME}"

if [[ $EUID -ne 0 ]]; then
    echo "This script must run as root (it writes to /etc/udev/rules.d)." >&2
    echo "Run:  sudo $0 $*" >&2
    exit 1
fi

if [[ "${1:-}" == "--remove" ]]; then
    rm -f "$RULE_DST"
    udevadm control --reload
    udevadm trigger --subsystem-match=input
    echo "Removed $RULE_DST"
    exit 0
fi

if [[ ! -f "$RULE_SRC" ]]; then
    echo "Rule file not found next to this script: $RULE_SRC" >&2
    exit 1
fi

# uaccess ACLs are applied by systemd-logind; warn on non-systemd systems.
if [[ ! -d /run/systemd/system ]]; then
    echo "WARNING: systemd-logind not detected — the 'uaccess' tag will have no effect." >&2
    echo "Fall back to the 'input' group instead:  sudo usermod -aG input \$USER" >&2
fi

install -m 644 "$RULE_SRC" "$RULE_DST"
udevadm control --reload
udevadm trigger --subsystem-match=input

echo "Installed $RULE_DST"
echo "Input devices are now readable by the active desktop session."
echo "If TeknoParrot is already running, restart it. No logout needed."
