from pathlib import Path
import csv,sys
root=Path(sys.argv[1]) if len(sys.argv)>1 else Path(__file__).resolve().parents[2]/"Docs/Reviews/BoatMotionHull_20261028"
rows=list(csv.DictReader((root/"Motion-after.csv").open(encoding="utf-8-sig")))
poses=[r for r in rows if r["phase"] in {"Map1-Boarding","Map1-Seating","Map1-Departing"}]
maximum=max(max(float(r["rightDeg"]),float(r["leftDeg"])) for r in poses)
assert maximum<45,maximum
assert all(r["hullGpu"]=="False" for r in rows)
assert all(r["hullVisible"]=="True" for r in rows)
print("PASS: wrist max",maximum,"degrees; moving hull is explicitly excluded from GPU driven rendering.")
