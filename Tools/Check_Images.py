import bpy

for name in ['ITEM1TO30.png', 'ITEM31TO60.png', 'ITEM61TO90.png']:
    p = r"d:\Unity Game Project\Eruption\TheLastEmpire\TheLastEmpire\Assets\Resources_Addressable\Image\UI\\" + name
    img = bpy.data.images.load(p)
    print(f"IMAGE: {name} => {img.size[0]} x {img.size[1]}")

