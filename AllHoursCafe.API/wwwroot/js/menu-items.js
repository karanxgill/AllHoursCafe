// menu-items.js - Custom script for menu items page
document.addEventListener('DOMContentLoaded', function() {
    console.log('Menu items script loaded');
    
    // Function to handle button clicks
    function handleButtonClick(event) {
        const target = event.target.closest('a');
        if (!target) return;
        
        const href = target.getAttribute('href');
        if (href) {
            console.log('Navigating to:', href);
            window.location.href = href;
        }
    }
    
    // Add click event listener to the entire table
    const table = document.querySelector('.admin-table');
    if (table) {
        table.addEventListener('click', handleButtonClick);
        console.log('Click handler added to table');
    }
    
    // Add click handlers to all action links directly
    const actionLinks = document.querySelectorAll('.actions a');
    actionLinks.forEach(link => {
        link.addEventListener('click', function(e) {
            e.preventDefault();
            const href = this.getAttribute('href');
            console.log('Action link clicked:', href);
            window.location.href = href;
        });
        console.log('Added handler to link:', link.getAttribute('href'));
    });
    
    // Add click handlers to all buttons
    const buttons = document.querySelectorAll('button');
    buttons.forEach(button => {
        button.addEventListener('click', function() {
            console.log('Button clicked:', this.textContent.trim());
        });
    });
});
