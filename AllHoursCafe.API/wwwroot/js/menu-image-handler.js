// General menu image handler
document.addEventListener('DOMContentLoaded', function() {
    console.log('Menu image handler loaded');

    // Find all menu item images
    const menuItems = document.querySelectorAll('.menu-item-card');
    console.log(`Found ${menuItems.length} menu items`);

    menuItems.forEach(item => {
        const itemName = item.querySelector('.item-name')?.textContent?.trim();
        const img = item.querySelector('img');

        if (img && itemName) {
            console.log(`Processing menu item: ${itemName}`);

            // Store the original src for fallback
            const originalSrc = img.src;

            // Add crossorigin attribute for external URLs
            if (originalSrc.startsWith('http')) {
                img.setAttribute('crossorigin', 'anonymous');
                console.log(`Added crossorigin attribute to ${itemName} image`);
            }

            // Create a robust fallback chain for images
            img.onerror = function() {
                console.error(`Image failed to load: ${this.src} for item: ${itemName}`);

                // If it's an external URL that failed, try a local fallback
                if (this.src.startsWith('http')) {
                    console.log(`External image failed, trying local fallback for ${itemName}`);
                    // Try to create a slug from the item name
                    const itemNameSlug = itemName.toLowerCase().replace(/[^a-z0-9]+/g, '-');

                    // Get the category if possible
                    const categorySection = item.closest('.category-section');
                    const categoryId = categorySection ? categorySection.getAttribute('data-category') : '';
                    const categoryName = getCategoryName(categoryId);

                    // Try category-specific path (without cache-busting query parameter)
                    this.src = `/images/Items/${categoryName}/${itemNameSlug}.jpg`;
                    return; // Exit and let the new src trigger onload or onerror again
                }

                // For local images that failed
                if (this.src.includes('/images/Items/')) {
                    // Try the root images folder
                    console.log(`Trying root images folder for ${itemName}`);
                    const itemNameSlug = itemName.toLowerCase().replace(/[^a-z0-9]+/g, '-');
                    this.src = `/images/${itemNameSlug}.jpg`;
                }
                else {
                    // Finally, use the placeholder
                    console.log(`Using placeholder for ${itemName}`);
                    this.src = '/images/menu/placeholder.jpg';
                }
            };

            // Add a load event to confirm success
            img.onload = function() {
                console.log(`Successfully loaded image for ${itemName}: ${this.src}`);
            };
        }
    });

    // Helper function to get category name from ID
    function getCategoryName(categoryId) {
        // Map category IDs to names (this should be dynamically populated in a real app)
        const categoryMap = {
            '1': 'breakfast',
            '2': 'lunch',
            '3': 'dinner',
            '4': 'beverages',
            '5': 'dessert',
            '6': 'snacks'
        };

        return categoryMap[categoryId] || 'other';
    }
});
