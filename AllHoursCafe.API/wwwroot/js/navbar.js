document.addEventListener('DOMContentLoaded', function() {
    const header = document.querySelector('header');
    const scrollThreshold = 50;

    if (!header) {
        console.error('Header element not found!');
        return;
    }

    function handleScroll() {
        if (window.scrollY > scrollThreshold) {
            if (!header.classList.contains('shrink')) {
                header.classList.add('shrink');
                header.classList.add('scrolled');
            }
        } else {
            if (header.classList.contains('shrink')) {
                header.classList.remove('shrink');
                header.classList.remove('scrolled');
            }
        }
    }

    // Add scroll event listener with throttling
    let ticking = false;
    window.addEventListener('scroll', function() {
        if (!ticking) {
            window.requestAnimationFrame(function() {
                handleScroll();
                ticking = false;
            });
            ticking = true;
        }
    });

    // Check initial scroll position
    handleScroll();

    // Mobile menu toggle
    const mobileMenuToggle = document.querySelector('.mobile-menu-toggle');
    const navLeft = document.querySelector('.nav-left');
    const navRight = document.querySelector('.nav-right');
    const navbar = document.querySelector('.navbar');

    if (mobileMenuToggle) {
        mobileMenuToggle.addEventListener('click', function(event) {
            event.preventDefault();
            event.stopPropagation();

            // Toggle menu visibility
            navLeft.classList.toggle('show');
            navRight.classList.toggle('show');

            // Add a class to the navbar to adjust its height when menu is open
            navbar.classList.toggle('menu-open');

            // Toggle icon between bars and times
            const icon = mobileMenuToggle.querySelector('i');
            if (icon.classList.contains('fa-bars')) {
                icon.classList.remove('fa-bars');
                icon.classList.add('fa-times');
            } else {
                icon.classList.remove('fa-times');
                icon.classList.add('fa-bars');
            }
        });
    } else {
        console.error('Mobile menu toggle not found');
    }

    // Close mobile menu when clicking outside
    document.addEventListener('click', function(event) {
        if (!event.target.closest('.navbar') &&
            !event.target.closest('.mobile-menu-toggle') &&
            navLeft.classList.contains('show')) {
            navLeft.classList.remove('show');
            navRight.classList.remove('show');
            navbar.classList.remove('menu-open');

            const icon = mobileMenuToggle.querySelector('i');
            icon.classList.remove('fa-times');
            icon.classList.add('fa-bars');
        }
    });

    // Close mobile menu when window is resized to desktop size
    window.addEventListener('resize', function() {
        if (window.innerWidth > 1250 && navLeft.classList.contains('show')) {
            navLeft.classList.remove('show');
            navRight.classList.remove('show');
            navbar.classList.remove('menu-open');

            const icon = mobileMenuToggle.querySelector('i');
            icon.classList.remove('fa-times');
            icon.classList.add('fa-bars');
        }
    });

    // Add sticky class to sidebar/dropdown if present
    var stickySidebar = document.querySelector('.admin-sidebar, .menu-sidebar, .category-dropdown-content');
    if (stickySidebar) {
        stickySidebar.classList.add('sticky-sidebar');
    }
});
