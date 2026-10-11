from __future__ import annotations

from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FILES = [
    "LiberationFleet.Server/Infrastructure/Persistence/Repositories/ChatRepository.cs",
    "LiberationFleet.Server/Infrastructure/Persistence/Repositories/ForumRepository.cs",
    "LiberationFleet.Server/Infrastructure/Persistence/Repositories/GiftRepository.cs",
]

OLD_SELECT = ".Select(l => new { l.UserId, Username = l.User!.Username, l.User.AvatarResourceId })"
NEW_SELECT = ".Select(l => new { l.UserId, l.User!.DisplayName, l.User.Username, l.User.AvatarResourceId })"
OLD_RETURN = "return rows.Select(r => (r.UserId, r.Username, r.AvatarResourceId)).ToList();"
NEW_RETURN = "return rows.Select(r => (r.UserId, UserDisplay.Name(r.DisplayName, r.Username), r.AvatarResourceId)).ToList();"


def ensure_using(text: str) -> str:
    needle = "using LiberationFleet.Server.Application.Common;"
    if needle in text:
        return text
    lines = text.splitlines(True)
    insert_at = 0
    for i, line in enumerate(lines):
        if line.startswith("using "):
            insert_at = i + 1
    lines.insert(insert_at, needle + "\n")
    return "".join(lines)


def main() -> None:
    for rel in FILES:
        path = ROOT / rel
        text = path.read_text(encoding="utf-8")
        c1 = text.count(OLD_SELECT)
        c2 = text.count(OLD_RETURN)
        text = text.replace(OLD_SELECT, NEW_SELECT).replace(OLD_RETURN, NEW_RETURN)
        text = ensure_using(text)
        path.write_text(text, encoding="utf-8")
        print(f"{rel}: select x{c1}, return x{c2}")


if __name__ == "__main__":
    main()
