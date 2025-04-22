// Global image fallback handler
document.addEventListener('DOMContentLoaded', function() {
    console.log('Image fallback handler loaded');

    // Process all images on the page
    function processImages() {
        const images = document.querySelectorAll('img');
        console.log(`Processing ${images.length} images on the page`);

        images.forEach(img => {
            const src = img.getAttribute('src');
            const alt = img.getAttribute('alt') || 'Image';

            // Skip images that already have onerror handlers
            if (img.hasAttribute('data-fallback-processed')) {
                return;
            }

            // Mark as processed
            img.setAttribute('data-fallback-processed', 'true');

            // Add crossorigin attribute for external URLs
            if (src && src.startsWith('http')) {
                img.setAttribute('crossorigin', 'anonymous');
                console.log(`Added crossorigin attribute to ${alt} image: ${src}`);
            }

            // Set up error handler
            img.onerror = function() {
                console.error(`Image failed to load: ${this.src} (${alt})`);

                // Prevent infinite error loops
                if (this.hasAttribute('data-fallback-attempted')) {
                    console.log(`Already attempted fallback for ${alt}, using final placeholder`);
                    this.src = '/images/menu/placeholder.jpg';
                    return;
                }

                this.setAttribute('data-fallback-attempted', 'true');

                // Use placeholder image
                this.src = '/images/menu/placeholder.jpg';
            };
        });
    }

    // Process images initially
    processImages();

    // Also process images after any AJAX content loads
    // This uses a MutationObserver to detect DOM changes
    const observer = new MutationObserver(function(mutations) {
        mutations.forEach(function(mutation) {
            if (mutation.addedNodes && mutation.addedNodes.length > 0) {
                // Check if any of the added nodes are images or contain images
                let hasNewImages = false;
                mutation.addedNodes.forEach(node => {
                    if (node.nodeName === 'IMG') {
                        hasNewImages = true;
                    } else if (node.querySelectorAll) {
                        const images = node.querySelectorAll('img');
                        if (images.length > 0) {
                            hasNewImages = true;
                        }
                    }
                });

                if (hasNewImages) {
                    console.log('New images detected in DOM, processing...');
                    processImages();
                }
            }
        });
    });

    // Start observing the document with the configured parameters
    observer.observe(document.body, { childList: true, subtree: true });

    // Handle all image loading errors globally as a backup
    document.addEventListener('error', function(e) {
        const target = e.target;

        // Check if the error is from an image
        if (target.tagName.toLowerCase() === 'img') {
            console.error('Global error handler caught image failure:', target.src, 'Alt:', target.alt);

            // Only apply if our other handlers didn't catch it
            if (!target.hasAttribute('data-fallback-attempted')) {
                target.setAttribute('data-fallback-attempted', 'true');
                // Set fallback image with cache-busting
                target.src = '/images/menu/placeholder.jpg';
            }
        }
    }, true); // Use capture phase to catch all errors

    console.log('Image fallback handler initialized with mutation observer');
});
