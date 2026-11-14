# Dibuja los iconos de las solapas de tokenshop.dll (icons_alpha.png, icons_preview.png e icons.bin, de donde sale icons.h).
from PIL import Image, ImageDraw
import math
S=512  # canvas
def new(): 
    im=Image.new('L',(S,S),0); return im, ImageDraw.Draw(im)
def rot(im,deg): return im.rotate(deg,resample=Image.BICUBIC,center=(S/2,S/2))
def sword_shape(d, cx=256, top=40, bottom=472, blade=30, guard=110):
    # vertical sword, tip at top
    gy=bottom-120
    d.polygon([(cx,top),(cx+blade,top+60),(cx+blade,gy),(cx-blade,gy),(cx-blade,top+60)],fill=255)
    d.line([(cx,top+40),(cx,gy-10)],fill=120,width=8)   # fuller
    d.rounded_rectangle([cx-guard,gy,cx+guard,gy+34],radius=14,fill=255)
    d.rectangle([cx-16,gy+34,cx+16,bottom-40],fill=255)
    d.ellipse([cx-30,bottom-56,cx+30,bottom+4],fill=255)
def staff_shape(d, cx=256, top=36, bottom=476):
    d.rounded_rectangle([cx-14,top+110,cx+14,bottom],radius=10,fill=255)
    # orb with claws
    d.ellipse([cx-52,top+10,cx+52,top+114],fill=255)
    d.ellipse([cx-26,top+36,cx+26,top+88],fill=90)
    d.polygon([(cx-70,top+150),(cx-84,top+60),(cx-50,top+110)],fill=255)
    d.polygon([(cx+70,top+150),(cx+84,top+60),(cx+50,top+110)],fill=255)
    d.rounded_rectangle([cx-44,top+128,cx+44,top+158],radius=10,fill=255)
def sword():
    im,d=new(); sword_shape(d); return rot(im,-40)
def staff():
    im,d=new(); staff_shape(d); return rot(im,-35)
def bow():
    im,d=new()
    # bow arc (thick) on the left, string on right, arrow horizontal
    d.arc([40,40,430,472],start=-78,end=78,fill=255,width=40)
    x1=235+195*math.cos(math.radians(78)); y1=256-216*math.sin(math.radians(78)); y2=256+216*math.sin(math.radians(78))
    d.line([(x1,y1),(x1,y2)],fill=255,width=10)
    d.line([(60,256),(470,256)],fill=255,width=16)
    d.polygon([(500,256),(430,220),(430,292)],fill=255)
    d.polygon([(40,256),(90,222),(120,256),(90,290)],fill=255)
    return rot(im,45)
def book():
    im,d=new()
    d.rounded_rectangle([96,60,416,452],radius=24,fill=255)
    d.rectangle([96,60,150,452],fill=150)             # spine
    d.rectangle([150,410,416,430],fill=110)           # pages
    # emblem
    d.ellipse([220,170,350,300],fill=70)
    d.ellipse([250,200,320,270],fill=255)
    d.rectangle([200,340,370,362],fill=70)
    # clasp
    d.rectangle([390,230,440,280],fill=255)
    im=rot(im,-10)
    sm=im.resize((int(S*0.8),int(S*0.8)),Image.LANCZOS); out=Image.new('L',(S,S),0); out.paste(sm,(int(S*0.1),int(S*0.1))); return out
def crossed():
    a,d=new(); sword_shape(d,blade=24,guard=90); a=rot(a,-42)
    b,d=new(); staff_shape(d); b=rot(b,42)
    # dark outline where they cross: cut a border of b out of a
    from PIL import ImageFilter, ImageChops
    cut=b.filter(ImageFilter.MaxFilter(21)).point(lambda v:255 if v>20 else 0)
    a=ImageChops.multiply(a,ImageChops.invert(cut))
    return ImageChops.lighter(a,b)
def scepter():
    im,d=new(); cx=256
    d.rounded_rectangle([cx-16,150,cx+16,476],radius=10,fill=255)
    d.ellipse([cx-30,440,cx+30,500],fill=255)
    # head: crown like mace
    d.ellipse([cx-70,60,cx+70,200],fill=255)
    d.ellipse([cx-30,100,cx+30,160],fill=90)
    d.polygon([(cx,10),(cx-26,72),(cx+26,72)],fill=255)
    d.polygon([(cx-110,130),(cx-62,104),(cx-62,156)],fill=255)
    d.polygon([(cx+110,130),(cx+62,104),(cx+62,156)],fill=255)
    d.rounded_rectangle([cx-50,200,cx+50,232],radius=10,fill=255)
    d.rounded_rectangle([cx-34,250,cx+34,270],radius=8,fill=255)
    return rot(im,-35)
def fist():
    im,d=new()
    # fist seen from the front: four knuckles, thumb across, wrist cuff
    for i in range(4):
        x=100+i*80
        d.rounded_rectangle([x,110,x+72,270],radius=30,fill=255)
    d.rounded_rectangle([100,200,412,380],radius=46,fill=255)
    for i in range(1,4):
        x=100+i*80-4
        d.line([(x,130),(x,250)],fill=70,width=8)
    d.rounded_rectangle([70,250,300,330],radius=36,fill=255)     # thumb
    d.arc([70,250,300,330],start=-60,end=100,fill=70,width=8)
    d.line([(150,330),(296,330)],fill=70,width=8)
    d.rounded_rectangle([150,392,380,470],radius=16,fill=255)    # cuff
    d.line([(170,430),(360,430)],fill=110,width=10)
    sm=im.resize((int(S*0.88),int(S*0.88)),Image.LANCZOS); out=Image.new('L',(S,S),0); out.paste(sm,(int(S*0.06),int(S*0.04))); return out
icons=[sword(),staff(),bow(),book(),crossed(),scepter(),fist()]
N=48
small=[i.resize((N,N),Image.LANCZOS) for i in icons]
sheet=Image.new('L',(N*7,N),0)
for k,i in enumerate(small): sheet.paste(i,(k*N,0))
sheet.save('icons_alpha.png')
# preview: gold on dark at 4x and 1x
prev=Image.new('RGB',(N*7*4,N*4+N+20),(28,26,24))
gold=Image.new('RGB',(N*7,N),(255,210,60))
big=sheet.resize((N*7*4,N*4),Image.NEAREST)
prev.paste(Image.new('RGB',big.size,(255,210,60)),(0,0),big)
prev.paste(gold,(0,N*4+10),sheet)
prev.save('icons_preview.png')
open('icons.bin','wb').write(sheet.tobytes())  # los bytes de icons.h, por filas
