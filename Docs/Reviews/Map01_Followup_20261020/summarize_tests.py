"""Summarize actual NUnit XML; the last result for each full name wins."""
from collections import Counter, defaultdict
from hashlib import sha256
from pathlib import Path
import json
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parent


def summarize(names):
    cases, runs = {}, []
    for name in names:
        path = root / name
        if not path.exists():
            continue
        run = ET.parse(path).getroot()
        runs.append({"file": name, "sha256": sha256(path.read_bytes()).hexdigest(),
                     **{k: run.get(k) for k in ("result", "total", "passed", "failed", "skipped")}})
        for case in run.iter("test-case"):
            failure = case.find("failure")
            cases[case.get("fullname")] = {
                "result": case.get("result"), "source": name,
                "failure": "" if failure is None else "".join(failure.itertext()).strip()}
    return {"runs": runs, "counts": dict(Counter(c["result"] for c in cases.values())),
            "unique": len(cases), "cases": dict(sorted(cases.items()))}


manifest = {
    "dev_preflight": summarize(["dev-latest.xml", "dev-balance-fixed.xml"]),
    "merged": summarize(["merged-weapons-fixed.xml", "merged-regression.xml",
                          "fresh-weapons-before.xml", "merged-regression-fixes.xml", "map02-m16-fixed.xml"]),
}
(root / "TEST_MANIFEST.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
lines = ["# Test manifest — 21/10/2026", "",
         "Nguồn là NUnit XML thực tế. Mỗi fullname chỉ tính một lần, lấy lượt mới nhất trong thứ tự liệt kê. Các lượt tái hiện lỗi và kết quả trước merge không được cộng vào số đạt bản ghép.", ""]
for group, data in manifest.items():
    lines += [f"## {group}", "", f"{data['unique']} ca riêng biệt: {data['counts']}.", "",
              "| XML | Kết quả lượt chạy | Đạt | Lỗi | Bỏ qua |", "|---|---|---:|---:|---:|"]
    for run in data["runs"]:
        lines.append(f"| {run['file']} | {run['result']} | {run['passed']} | {run['failed']} | {run['skipped']} |")
    grouped = defaultdict(Counter)
    for name, case in data["cases"].items():
        grouped[name.rsplit(".", 1)[0]][case["result"]] += 1
    lines += ["", "| Lớp test (kết quả mới nhất) | Đạt | Lỗi | Bỏ qua |", "|---|---:|---:|---:|"]
    for name, counts in sorted(grouped.items()):
        lines.append(f"| {name} | {counts['Passed']} | {counts['Failed']} | {counts['Skipped']} |")
    for name, case in data["cases"].items():
        if case["result"] == "Failed":
            lines += ["", f"### {name}", "", case["failure"]]
    lines += [""]
(root / "TEST_MANIFEST.md").write_text("\n".join(lines), encoding="utf-8")
print(json.dumps({key: {"unique": value["unique"], **value["counts"]} for key, value in manifest.items()}))
