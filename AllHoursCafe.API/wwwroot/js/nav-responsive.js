// nav-responsive.js
// Responsive navigation: show menu toggle on small screens, hide horizontal nav links, and fix right space

document.addEventListener('DOMContentLoaded', function () {
    const navbar = document.querySelector('.navbar');
    const navLeft = document.querySelector('.nav-left');
    const navRight = document.querySelector('.nav-right');
    const menuToggle = document.querySelector('.mobile-menu-toggle');

    let navOpen = false;
    function updateNav() {
        const winWidth = window.innerWidth;
        if (winWidth <= 1024) {
            menuToggle.style.display = 'block';
            if (!navOpen) {
                navLeft.style.display = 'none';
                navRight.style.display = 'none';
            }
        } else {
            navLeft.style.display = '';
            navRight.style.display = '';
            menuToggle.style.display = 'none';
            navOpen = false;
        }
    }

    menuToggle.addEventListener('click', function () {
        navOpen = !navOpen;
        if (navOpen) {
            navLeft.style.display = 'flex';
            navRight.style.display = 'flex';
        } else {
            navLeft.style.display = 'none';
            navRight.style.display = 'none';
        }
    });

    window.addEventListener('resize', updateNav);
    updateNav();
});
