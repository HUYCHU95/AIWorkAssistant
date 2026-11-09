import {readFile} from 'node:fs/promises';
import vm from 'node:vm';
import assert from 'node:assert/strict';
export async function run(){
 const names=['site','dashboard','ai-entry','chat','markdown'];
 for(const name of names)new vm.Script(await readFile(new URL('../src/AIWork.Web/wwwroot/js/'+name+'.js',import.meta.url),'utf8'),{filename:name+'.js'});
 class Node {constructor(tag,text=''){this.tag=tag;this.textContent=text;this.children=[];}append(...nodes){this.children.push(...nodes);}replaceChildren(...nodes){this.children=nodes;}}
 const context={window:{},document:{createElement:t=>new Node(t),createTextNode:t=>new Node('#text',t)},location:{origin:'http://127.0.0.1:5088'},URL,navigator:{clipboard:{writeText:()=>Promise.resolve()}}};vm.createContext(context);vm.runInContext(await readFile(new URL('../src/AIWork.Web/wwwroot/js/markdown.js',import.meta.url),'utf8'),context);
 const target=new Node('div');context.window.renderMarkdown(target,'<img src=x onerror=alert(1)>\n[bad](javascript:alert)\n[ok](/Tasks)\n```html\n<script>unsafe text</script>\n```\n| A | B |\n| --- | --- |\n| 1 | 2 |');
 const all=[];function walk(node){if(!node||typeof node!=='object')return;all.push(node);node.children.forEach(walk);}walk(target);assert(!all.some(n=>['img','script','iframe'].includes(n.tag)));assert(all.some(n=>n.tag==='code'&&n.textContent.includes('<script>')));assert(all.some(n=>n.tag==='table'));assert(all.filter(n=>n.tag==='a').every(n=>!n.href.startsWith('javascript:')));
 return 'PASS: five JavaScript parsers; Markdown HTML/XSS/URL/code/table checks (isolated unit fixture).';
}
