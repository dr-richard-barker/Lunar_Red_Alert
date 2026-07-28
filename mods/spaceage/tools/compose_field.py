import numpy as np, os
from PIL import Image
src = "../artwork/lunar/tiles"
def L(n): return Image.open(os.path.join(src,n)).convert("RGB")
reg = [L("00_Regolith.png"), L("01_Regolith.png"), L("02_Regolith.png")]
basalt, ice, cfloor, cwall = L("03_Basalt.png"), L("04_IceDeposit.png"), L("05_CraterFloor.png"), L("06_CraterWall.png")
t = reg[0].width; cols, rows = 22, 12
rng = np.random.default_rng(7)
grid = np.zeros((rows, cols), int)
yy,xx=np.mgrid[0:rows,0:cols]
for _ in range(5):
    cy,cx,r = rng.integers(0,rows), rng.integers(0,cols), rng.integers(1,3)
    grid[(np.hypot(xx-cx,yy-cy)<=r)&(grid==0)]=1
for _ in range(6):
    cy,cx,r = rng.integers(2,rows-2), rng.integers(2,cols-2), rng.integers(2,4)
    d=np.hypot(xx-cx,yy-cy); grid[(d<=r)&(d>r-1)]=4; grid[d<=r-1]=3
for _ in range(9):
    y,x=rng.integers(0,rows),rng.integers(0,cols)
    if grid[y,x]==0: grid[y,x]=2
def variant(img, code):
    if code in (0,1,3):  # regolith-ish: randomize orientation to kill grid repetition
        img = img.rotate(int(rng.integers(0,4))*90)
        if rng.random()<0.5: img = img.transpose(Image.FLIP_LEFT_RIGHT)
    return img
canvas = Image.new("RGB",(cols*t,rows*t))
pick = {0:lambda:reg[rng.integers(0,3)],1:lambda:basalt,2:lambda:ice,3:lambda:cfloor,4:lambda:cwall}
for y in range(rows):
    for x in range(cols):
        canvas.paste(variant(pick[grid[y,x]](), grid[y,x]),(x*t,y*t))
canvas.save("../artwork/lunar_battlefield.png")
proof = Image.new("RGB",(t*3,t*3))
for y in range(3):
    for x in range(3): proof.paste(reg[0],(x*t,y*t))
proof.save("../artwork/regolith_tiling_proof.png")
print("recomposed battlefield %dx%d + tiling proof"%(cols*t,rows*t))
