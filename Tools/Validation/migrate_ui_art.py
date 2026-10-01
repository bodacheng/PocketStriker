#!/usr/bin/env python3
"""Scoped, reviewable UI migration. Changes visual fields only; preserves geometry and events."""
from pathlib import Path
import re, json, hashlib, argparse
ROOT=Path(__file__).resolve().parents[2] if Path(__file__).resolve().parts[-3:-1]==('Tools','Validation') else Path('/Users/daisei/PocketStriker')
FILL='c541b21ee2004b9aacbd84ee924843af'; OUTLINE='efb71ca1ee684dd8874d7c61ac1f97ca'
COLORS={'background':(.035,.050,.066),'panel':(.070,.105,.135),'outline':(.38,.48,.50),
        'selection':(.91,.70,.32),'secondary':(.14,.24,.29),'primary':(.49,.35,.15),'danger':(.42,.14,.17)}
INK=(.95,.94,.86); GOLD=(.96,.78,.43); MUTED=(.68,.76,.79)

def match_guid(s):
 m=re.search(r'guid: ([a-f0-9]{32})',s);return m[1] if m else None

def guid_map():
 result={}
 for p in (ROOT/'Assets/OrganizedResources/InUse/D_Asset').rglob('*.meta'):
  if p.suffix=='.meta' and p.name.lower().endswith(('.png.meta','.jpg.meta','.psd.meta','.tga.meta')):
   g=match_guid(p.read_text(errors='ignore'))
   if g:result[g]=str(p.relative_to(ROOT))[:-5]
 return result

def category(p):
 if not p:return None
 if '/Demo_RnakBadge/' in p or '/Demo_RankBadge/' in p:return None
 n=Path(p).name.lower();p=p.lower()
 # These two assets encode distinct skill-set validity, including their legend.
 if n in ('frame_iconframe01_green.png','frame_iconframe01_red.png'):return None
 if 'shift - complete sci-fi ui' in p:
  if '/border/' in p and '/circle/' not in p:
   if 'filled' in n:return 'panel'
   return 'outline'
  if n=='shiny move.png':return 'shimmer'
  if '/icon/' in p:return None
 elif 'gui pro kit - sci-fi' in p:
  if '/frame_' in p or '/frame_custom/' in p or '/frame_demo/' in p:
   if any(x in n for x in ['white_border','_s_','iconframe']):return 'outline'
   if 'frame_frame05' in n:return None
   return 'panel'
  if '/popup/' in p:return 'panel'
  if '/button_' in p:return 'panel'
  if '/label/' in p:return 'panel'
  if '/sliders' in p:return 'panel'
 elif 'sci-fi buttons and panels' in p:
  if n.startswith(('panel','button','btn')):return 'panel'
 elif 'virtual simulation ui pack' in p:
  if n.startswith('panel'):return 'panel'
  if n.startswith('btn_closepage'):return None
  if n.startswith(('btn_','sliderbg','label01')):return 'panel'
  if n=='titlebg_loading.png':return 'background'
 return None

def getid(doc,field):
 m=re.search(r'^  '+re.escape(field)+r': \{fileID: (-?\d+)',doc,re.M);return m[1] if m else None

def getcolor(doc):
 m=re.search(r'^  m_Color: \{r: ([\d.e+-]+), g: ([\d.e+-]+), b: ([\d.e+-]+), a: ([\d.e+-]+)\}',doc,re.M)
 return tuple(map(float,m.groups())) if m else (1,1,1,1)

def col(c,a=1):return '{r: %.6g, g: %.6g, b: %.6g, a: %.6g}'%(*c,a)
def put(doc,key,value):return re.sub(r'(^  '+re.escape(key)+r': )[^\n]*',lambda m:m[1]+str(value),doc,flags=re.M)
def sprite(g):return '{fileID: 21300000, guid: '+g+', type: 3}'

def process(p,gmap):
 s=p.read_text();docs=re.split(r'(?=^--- !u!)',s,flags=re.M)
 byid={};gos={};trans={}
 for i,d in enumerate(docs):
  m=re.match(r'--- !u!\d+ &(-?\d+)',d)
  if not m:continue
  byid[m[1]]=i
  if '\nGameObject:\n' in d:
   nm=re.search(r'^  m_Name: (.*)',d,re.M);gos[m[1]]=nm[1] if nm else ''
  if '\nRectTransform:\n' in d or '\nTransform:\n' in d:trans[getid(d,'m_GameObject')]=(m[1],getid(d,'m_Father'))
 def path(go):
  names=[];seen=set()
  while go and go not in seen:
   seen.add(go);names.append(gos.get(go,'?'));tr=trans.get(go)
   if not tr:break
   parent=tr[1];go=getid(docs[byid[parent]],'m_GameObject') if parent in byid else None
  return '/'.join(reversed(names))
 def ischild(go,parentgo):
  return path(go).startswith(path(parentgo)+'/') or go==parentgo
 buttons={}
 for id,i in byid.items():
  d=docs[i]
  if 'm_TargetGraphic:' in d and 'm_OnClick:' in d:buttons[id]=(getid(d,'m_GameObject'),getid(d,'m_TargetGraphic'))
 image_buttons={target:bid for bid,(go,target) in buttons.items()}
 changes=[];changed_images={};theme_buttons=set()
 for id,i in byid.items():
  d=docs[i]
  if '  m_Sprite:' not in d or '  m_FillMethod:' not in d:continue
  go=getid(d,'m_GameObject');imagepath=path(go);oldg=match_guid(re.search(r'^  m_Sprite: (.*)',d,re.M)[1]);oldasset=gmap.get(oldg,'');role=category(oldasset)
  bid=image_buttons.get(id)
  # Large transparent catch-all controls and combat effect-only targets remain untouched.
  pure_button=bid and not oldg and getcolor(d)[3]==0 and not any(t in imagepath.lower() for t in ['curtain','whole','fullscreen','fighter','textinput','closeui','touch'])
  if pure_button:
   tr=trans.get(go);rd=docs[byid[tr[0]]] if tr and tr[0] in byid else ''
   size=re.search(r'm_SizeDelta: \{x: ([\d.e+-]+), y: ([\d.e+-]+)',rd)
   pure_button=size and float(size[1])>0 and float(size[2])>0 and float(size[1])<600 and float(size[2])<240
  if not role and not pure_button:continue
  # Keep battle rings/joystick/gems, specialised preparation & HUD already have their own skin.
  if 'FightingStepLayer' in str(p) or 'FightPrepareLayer' in str(p) or 'InBattleEnvolve' in str(p) or 'StoneProperty' in str(p):continue
  lower=imagepath.lower()
  if role=='shimmer':
   d=put(d,'m_Color',col((1,1,1),0));d=put(d,'m_RaycastTarget',0)
  else:
   original=getcolor(d); tone=role or 'secondary'
   if bid:
    tone='secondary';bn=path(buttons[bid][0]).lower()
    if any(x in bn for x in ['delete','sell','quit','no_btn','nobtn']):tone='danger'
    elif any(x in bn for x in ['confirm','yes','claim','gacha','fightbtn','fightbegin','buy','updatebtn','nextbtn','okbtn']):tone='primary'
    theme_buttons.add(bid)
   elif role=='panel' and any(x in lower for x in ['selected','selectframe','indicator','chosen']):tone='selection'
   elif role=='outline' and any(x in lower for x in ['selected','selectframe','indicator','chosen']):tone='selection'
   elif role=='panel' and any(x in lower for x in ['/bg','background','imagebg']):tone='background' if any(x in lower for x in ['titlebg','stoneupdatesconfirm','layer/bg','layer/background']) else 'panel'
   ng=OUTLINE if role=='outline' else FILL
   d=put(d,'m_Sprite',sprite(ng));d=put(d,'m_Type',1);d=put(d,'m_PixelsPerUnitMultiplier',1)
   # Invisible region placeholders remain invisible; textured buttons intentionally become visible.
   alpha=1 if pure_button else original[3]
   d=put(d,'m_Color',col(COLORS[tone],alpha));changed_images[id]=(ng,tone)
  if d!=docs[i]:
   changes.append({'object':imagepath,'component':id,'kind':'image','source':oldasset or '(transparent button)','role':role or 'secondary','beforeSha256':hashlib.sha256(docs[i].encode()).hexdigest()});docs[i]=d
 # Keep authored geometry and text layout; only remove neon/low-contrast color and glow.
 for id,i in byid.items():
  d=docs[i];go=getid(d,'m_GameObject')
  if '  m_FontData:' not in d:continue
  if 'FightingStepLayer' in str(p) or 'FightPrepareLayer' in str(p) or 'InBattleEnvolve' in str(p) or 'StoneProperty' in str(p):continue
  c=getcolor(d);rgb=c[:3];textpath=path(go).lower();tone=None
  for bid in theme_buttons:
   if ischild(go,buttons[bid][0]):tone=changed_images.get(buttons[bid][1],(None,'secondary'))[1];break
  neon=(max(rgb)-min(rgb)>.5 and max(rgb)>.65) or (max(rgb)<.45 and c[3]>.5)
  if tone or neon:
   ink=INK
   if not tone and any(x in textpath for x in ['currency','price','cost','remain','level','reward','coin','diamond','gemcount','time']):ink=GOLD
   nd=put(d,'m_Color',col(ink,c[3]))
   if nd!=d:docs[i]=nd;changes.append({'object':path(go),'component':id,'kind':'text-ink'})
 for bid in theme_buttons:
  i=byid[bid];d=docs[i]
  d=put(d,'m_Transition',1);d=put(d,'disableTransitionOverride',1)
  for name,value in {'m_NormalColor':(1,1,1,1),'m_HighlightedColor':(1.16,1.12,1.04,1),'m_PressedColor':(.72,.72,.72,1),'m_SelectedColor':(1.12,1.10,1.04,1),'m_DisabledColor':(.52,.55,.58,.8)}.items():
   d=re.sub(r'(^    '+name+r': )[^\n]*',lambda m:m[1]+col(value[:3],value[3]),d,flags=re.M)
  d=re.sub(r'(^    m_ColorMultiplier: )[^\n]*',r'\g<1>1',d,flags=re.M)
  target=buttons[bid][1];ng=changed_images.get(target,(FILL,None))[0]
  if 'disableSprite:' in d and match_guid(re.search(r'^  disableSprite: (.*)',d,re.M)[1]):d=put(d,'disableSprite',sprite(ng))
  for name in ['m_HighlightedSprite','m_PressedSprite','m_SelectedSprite','m_DisabledSprite']:
   d=re.sub(r'(^    '+name+r': )[^\n]*',lambda m:m[1]+'{fileID: 0}',d,flags=re.M)
  docs[i]=d;changes.append({'object':path(buttons[bid][0]),'component':bid,'kind':'button-states'})
 # Skin inherited prefab sprite overrides, including their alternate disabled sprite references.
 for i,d in enumerate(docs):
  if '\nPrefabInstance:' not in d:continue
  def change_override(m):
   role=category(gmap.get(m[2],''))
   if role in ('outline','panel','background'):return m[1]+(OUTLINE if role=='outline' else FILL)+m[3]
   return m[0]
  docs[i]=re.sub(r'(objectReference: \{fileID: 21300000, guid: )([a-f0-9]{32})(, type: 3\})',change_override,d)
 result=''.join(docs)
 return result,changes

if __name__=='__main__':
 parser=argparse.ArgumentParser();parser.add_argument('--apply',action='store_true');args=parser.parse_args()
 gmap=guid_map();results=[]
 for p in sorted((ROOT/'Assets/Resources/DummyLayerSystem').rglob('*.prefab')):
  old=p.read_text();new,changes=process(p,gmap)
  if new==old:continue
  name=str(p.relative_to(ROOT));entry={'prefab':name,'sha256Before':hashlib.sha256(old.encode()).hexdigest(),'sha256After':hashlib.sha256(new.encode()).hexdigest(),'changes':changes};results.append(entry)
  if args.apply:
   backup=ROOT/'Logs/UIArt/PrefabBaseline'/name;backup.parent.mkdir(parents=True,exist_ok=True)
   if not backup.exists():backup.write_text(old)
   p.write_text(new)
 out=ROOT/'Logs/UIArt' if args.apply else Path('/Users/daisei/Documents/Codex/2026-09-30/task-2/UIArt')
 out.mkdir(parents=True,exist_ok=True)
 report={'applied':args.apply,'scope':'Presentation-only fields in Resources UI prefabs; geometry, events, gameplay sprites and existing preparation/HUD/evolution presentation preserved.','prefabs':len(results),'changes':sum(len(x['changes']) for x in results),'entries':results}
 (out/'migration.json').write_text(json.dumps(report,indent=2,ensure_ascii=False))
 print(json.dumps({k:v for k,v in report.items() if k!='entries'},ensure_ascii=False))
 print('\n'.join(x['prefab'] for x in results))
