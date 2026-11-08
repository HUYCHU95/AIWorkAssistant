(() => {
 const menu=document.getElementById('nav-toggle'), nav=document.getElementById('primary-navigation');
 menu?.addEventListener('click',()=>{const open=menu.getAttribute('aria-expanded')!=='true';menu.setAttribute('aria-expanded',String(open));nav?.classList.toggle('navigation-open',open);});
 document.getElementById('sidebar-collapse')?.addEventListener('click',()=>{document.body.classList.toggle('sidebar-compact');localStorage.setItem('aiwork-sidebar',document.body.classList.contains('sidebar-compact')?'compact':'full');});
 if(localStorage.getItem('aiwork-sidebar')==='compact')document.body.classList.add('sidebar-compact');
 document.querySelectorAll('#primary-navigation a').forEach(a=>{if(new URL(a.href).pathname===location.pathname&&!a.href.includes('?'))a.classList.add('active');});
 window.aiwork={token:()=>document.querySelector('input[name="__RequestVerificationToken"]')?.value??'',text:(tag,value,cls)=>{const el=document.createElement(tag);el.textContent=value??'';if(cls)el.className=cls;return el;}};
})();
