#!/usr/bin/env bash
# Prints release notes for a tag: the commits since the previous v* tag, grouped by the first word of
# their subject (Add… → New features, Fix… → Bug fixes, the rest → Other changes), each with the
# "- " bullet lines from its message body. Works for commits pushed straight to main, unlike
# `gh release create --generate-notes`, which only lists merged pull requests.
#
#   .github/release-notes.sh v1.2.0 > notes.md
set -euo pipefail

tag="${1:?usage: release-notes.sh <tag>}"
repo="${GITHUB_REPOSITORY:-AlxndrJhn/BrowserFork}"
prev=$(git describe --tags --abbrev=0 --match 'v*' "$tag^" 2>/dev/null || true)

git log --no-merges --format='@@@%h %s%n%b' "${prev:+$prev..}$tag" | awk -v repo="$repo" -v prev="$prev" -v tag="$tag" '
function flush() {
    if (subject == "") return
    entry = "- " subject " (" hash ")\n" details
    if (subject ~ /^(Fix|Fixes|Fixed|Correct|Repair)[ :]/) fixes = fixes entry
    else if (subject ~ /^(Add|Adds|Added|Allow|Implement|Introduce|Support|Show|Let)[ :]/) features = features entry
    else other = other entry
    subject = ""; details = ""; inBullet = 0
}
/^@@@/ {
    flush()
    hash = substr($1, 4)
    subject = substr($0, length($1) + 2)
    next
}
/^(Co-Authored-By|Signed-off-by):/ { inBullet = 0; next }
/^[-*] / { details = details "  - " substr($0, 3) "\n"; inBullet = 1; next }
# wrapped continuation of a body bullet
inBullet && /^  +[^ ]/ { sub(/^ +/, ""); details = substr(details, 1, length(details) - 1) " " $0 "\n"; next }
{ inBullet = 0 }
END {
    flush()
    if (features != "") printf "## New features\n\n%s\n", features
    if (fixes != "") printf "## Bug fixes\n\n%s\n", fixes
    if (other != "") printf "## Other changes\n\n%s\n", other
    print "## Install\n\nRun **BrowserFork-Setup.exe** (needs the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)). Installs made with the setup update themselves from the tray icon.\n"
    if (prev != "") printf "**Full Changelog**: https://github.com/%s/compare/%s...%s\n", repo, prev, tag
}'
