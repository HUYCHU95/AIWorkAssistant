(() => {
    const button = document.getElementById('nav-toggle');
    const navigation = document.getElementById('primary-navigation');
    if (!button || !navigation) return;
    button.addEventListener('click', () => {
        const expanded = button.getAttribute('aria-expanded') !== 'true';
        button.setAttribute('aria-expanded', String(expanded));
        navigation.classList.toggle('navigation-open', expanded);
    });
})();
