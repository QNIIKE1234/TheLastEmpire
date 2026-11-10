"""
Removes the obsolete disabled 'Capsule' placeholder child GameObject and its components
from all 22 Enemy prefabs in Assets/Resources_Addressable/Prefabs/Character/.
"""

import os
import re

PREFAB_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "Assets", "Resources_Addressable", "Prefabs", "Character"))

ENEMY_PREFABS = [
    # Zombies
    "ZombieLeader", "ZombieTank", "ZombieNormal", "ZombieRunner", "ZombieDog", "ZombieBoomer", "ZombieSpitter",
    # Robots
    "RobotNormal", "RobotBlade", "RobotDrone", "RobotGunner", "RobotKamikaze", "RobotLaser", "RobotTanker", "RobotLeader",
    # Aliens
    "AlienNormal", "AlienCrawler", "AlienHook", "AlienInvisible", "AlienSmart", "AlienSpitter", "AlienLeader"
]


def remove_capsule_from_prefab(prefab_name):
    path = os.path.join(PREFAB_DIR, f"{prefab_name}.prefab")
    if not os.path.exists(path):
        print(f"Skipping {prefab_name} (file not found)")
        return

    with open(path, "r", encoding="utf-8") as f:
        content = f.read()

    # Split into YAML documents
    docs = content.split("\n--- ")
    header = docs[0]

    # 1. Find Capsule GameObject document and its component IDs
    capsule_go_id = None
    capsule_comp_ids = set()

    for doc in docs[1:]:
        full_doc = "--- " + doc
        if re.search(r"^GameObject:\n(?:  [^\n]*\n)*?  m_Name: Capsule\b", full_doc, re.MULTILINE):
            m_id = re.match(r"^--- !u!1 &(\d+)", full_doc)
            if m_id:
                capsule_go_id = m_id.group(1)
            # Find all component fileIDs in this GameObject
            for comp_m in re.finditer(r"- component: \{fileID:\s*(\d+)\}", full_doc):
                capsule_comp_ids.add(comp_m.group(1))
            break

    if not capsule_go_id:
        print(f"[{prefab_name}] No Capsule GameObject found.")
        return

    all_drop_ids = {capsule_go_id} | capsule_comp_ids
    print(f"[{prefab_name}] Found Capsule GO {capsule_go_id} with components {capsule_comp_ids}")

    # 2. Filter out documents and clean up parent Transform children references
    kept_docs = []
    dropped_count = 0

    for doc in docs[1:]:
        full_doc = "--- " + doc
        m_doc_id = re.match(r"^--- !u!\d+ &(\d+)", full_doc)
        if m_doc_id and m_doc_id.group(1) in all_drop_ids:
            dropped_count += 1
            continue

        # In any Transform / RectTransform, remove any child reference to capsule components
        for comp_id in capsule_comp_ids:
            full_doc = re.sub(rf"  - \{{fileID:\s*{comp_id}\}}\n?", "", full_doc)

        kept_docs.append(full_doc.strip())

    final_content = header + "\n" + "\n".join(kept_docs) + "\n"

    with open(path, "w", encoding="utf-8") as f:
        f.write(final_content)

    print(f"[{prefab_name}] Successfully removed Capsule (dropped {dropped_count} documents)")


def main():
    print("==================================================")
    print("Removing Old Capsule Placeholders From Enemy Prefabs")
    print("==================================================")

    for p in ENEMY_PREFABS:
        remove_capsule_from_prefab(p)

    print("==================================================")
    print("Completed Capsule cleanup across all enemy prefabs.")


if __name__ == "__main__":
    main()
