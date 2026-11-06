"""Copy sliced sprites into the Unity project with stable .meta files and write sprites.json.

  python install_sprites.py --slice-dir .codex_tmp/slice/page --dest Assets/Resources/Sprites/Page [--ref 800] [--only a,b]

Each part's folder/file/border/readable come from the slice spec (stored in layout.json).
Existing .meta files keep their GUID and settings; only spriteBorder/isReadable are updated,
so prefabs that already reference the sprite stay valid when an image is replaced.
"""
import argparse, hashlib, json, os, re, shutil

FOLDER_META = 'fileFormatVersion: 2\nguid: {g}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
TEX_META = '''fileFormatVersion: 2
guid: {GUID}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {}
  serializedVersion: 12
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMasterTextureLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 0
  alignment: 0
  spritePivot: {x: 0.5, y: 0.5}
  spritePixelsToUnits: 100
  spriteBorder: {x: {B}, y: {B}, z: {B}, w: {B}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  - serializedVersion: 3
    buildTarget: Standalone
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: 5e97eb03825dee720800000000000000
    internalID: 0
    vertices: []
    indices: 
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {}
  spritePackingTag: 
  pSDRemoveMatte: 0
  pSDShowRemoveMatteOption: 0
  userData: 
  assetBundleName: 
  assetBundleVariant:'''


def guid_for(path):
    return hashlib.md5(('slime-ui:' + path).encode('utf-8')).hexdigest()


def write(path, text):
    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        f.write(text)


def ensure_dir(d):
    parts = d.replace('\\', '/').split('/')
    for i in range(2, len(parts) + 1):
        sub = '/'.join(parts[:i])
        os.makedirs(sub, exist_ok=True)
        if not os.path.exists(sub + '.meta'):
            write(sub + '.meta', FOLDER_META.format(g=guid_for(sub)))


def border_text(b):
    if isinstance(b, (list, tuple)):
        return '{x: %d, y: %d, z: %d, w: %d}' % tuple(b)
    return '{x: %d, y: %d, z: %d, w: %d}' % ((int(b or 0),) * 4)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--slice-dir', required=True)
    ap.add_argument('--dest', required=True)
    ap.add_argument('--ref', type=float, default=800, help='reference canvas width the preview maps to')
    ap.add_argument('--only')
    a = ap.parse_args()
    lay = json.load(open(os.path.join(a.slice_dir, 'layout.json'), encoding='utf-8'))
    W, H = lay['size']
    k = a.ref / W
    only = set(a.only.split(',')) if a.only else None
    out_path = os.path.join(a.slice_dir, 'sprites.json')
    sprites = json.load(open(out_path, encoding='utf-8')) if os.path.exists(out_path) else {}
    rows = []
    for name, p in lay['parts'].items():
        if only and name not in only:
            continue
        folder = p.get('folder', '')
        d = a.dest.rstrip('/') + ('/' + folder if folder else '')
        ensure_dir(d)
        dst = '%s/%s.png' % (d, p.get('file', name))
        shutil.copyfile(os.path.join(a.slice_dir, name + '.png'), dst)
        mp = dst + '.meta'
        if os.path.exists(mp):
            m = open(mp, encoding='utf-8').read()
        else:
            m = TEX_META.replace('{GUID}', guid_for(dst)).replace('{B}', '0')
        m = re.sub(r'spriteBorder: \{[^}]*\}', 'spriteBorder: ' + border_text(p.get('border', 0)), m)
        m = re.sub(r'isReadable: \d', 'isReadable: %d' % (1 if p.get('readable') else 0), m)
        write(mp, m)
        guid = re.search(r'guid: (\w+)', m).group(1)
        cx = (p['x'] + p['w'] / 2 - W / 2) * k
        cy = (H / 2 - (p['y'] + p['h'] / 2)) * k
        sprites[name] = {'asset': dst, 'guid': guid, 'ref_size': [round(p['w'] * k), round(p['h'] * k)],
                         'ref_center': [round(cx), round(cy)], 'source_rect': [p['x'], p['y'], p['w'], p['h']], 'border': p.get('border', 0)}
        rows.append('| %s | %dx%d | %dx%d | (%d, %d) | %s |' % (dst[len(a.dest) + 1:], p['w'], p['h'], *sprites[name]['ref_size'], *sprites[name]['ref_center'], p.get('border') or '-'))
    json.dump(sprites, open(out_path, 'w', encoding='utf-8'), indent=1, ensure_ascii=False)
    print('| file | source | ref size | ref center | border |\n| --- | --- | --- | --- | --- |')
    print('\n'.join(rows))
    print('sprites.json ->', out_path)


if __name__ == '__main__':
    main()
