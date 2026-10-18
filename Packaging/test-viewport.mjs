import fs from 'node:fs';
import assert from 'node:assert/strict';

// Exercise the actual viewport script with a deterministic GPU boundary.
// This covers async scene ownership and coordinate conversion, not visual quality.
const html = fs.readFileSync(new URL('../Aurora.Editor/Assets/viewport.html', import.meta.url), 'utf8');
const script = html.match(/<script>([\s\S]*?)<\/script>/)[1];
const images = [], deleted = [], messages = [], reads = [];
let created = 0, pickId = 1, viewportSize;
class Texture {}
class ImageStub { constructor() { images.push(this); } }
const gpu = new Proxy({
  getShaderParameter: () => true, getProgramParameter: () => true,
  createTexture: () => { created++; return new Texture(); },
  deleteTexture: texture => deleted.push(texture),
  viewport: (...args) => viewportSize = args,
  readPixels: (...args) => { reads.push(args.slice(0, 4)); args[6].set([pickId, 0, 0, 255]); }
}, { get: (target, name) => name in target ? target[name] : /^[A-Z_0-9]+$/.test(name) ? 1 : () => ({}) });
const canvas = { clientWidth: 200, clientHeight: 100, width: 200, height: 100,
  getContext: () => gpu, setPointerCapture() {} };
const window = { devicePixelRatio: 2, chrome: { webview: { postMessage: message => messages.push(message) } } };
const document = { getElementById: () => canvas };
const api = new Function('document', 'window', 'ResizeObserver', 'Image', 'WebGLTexture',
  script + '\nreturn {load,texture,selected:()=>selected};')(document, window, class { observe() {} }, ImageStub, Texture);
const scene = () => ({ Materials: ['body'], Nodes: [{
  Positions: [0,0,0, 1,0,0, 0,1,0], Normals: [0,0,1, 0,0,1, 0,0,1],
  Uv: [0,0, 1,0, 0,1], Indices: [0,1,2], Groups: [{ Material: 0, First: 0, Count: 3 }]
}] });
api.load(scene());
assert.deepEqual(viewportSize, [0, 0, 400, 200]);
api.texture('body', 'old-scene');
api.load(scene());
images[0].onload();
assert.equal(created, 0, 'stale image must not bind a texture into a new scene');
api.texture('body', 'first-map'); images[1].onload();
api.texture('body', 'replacement-map'); images[2].onload();
assert.equal(created, 2);
assert.equal(deleted.length, 1, 'replaced GPU texture must be released');
api.texture('body', 'slow-map'); api.texture('body', 'newer-map'); images[4].onload(); images[3].onload();
assert.equal(created, 3, 'late map in the same scene must not replace a newer request');
canvas.onpointerdown({clientX:20, clientY:30, pointerId:1});
canvas.onpointerup({clientX:20, clientY:30, offsetX:20, offsetY:30});
assert.deepEqual(reads.at(-1), [40,139,1,1]);
assert.equal(api.selected(), 0);
pickId = 0;
canvas.onpointerdown({clientX:20, clientY:30, pointerId:1});
canvas.onpointerup({clientX:20, clientY:30, offsetX:20, offsetY:30});
assert.equal(api.selected(), -1, 'background click clears the object selection');
assert.deepEqual(messages.at(-1), {selected:-1});
api.load(scene());
assert.equal(deleted.length, 3, 'scene replacement releases remaining GPU textures');
console.log('PASS viewport scene ownership, texture lifetime, DPI picking and background deselection');
