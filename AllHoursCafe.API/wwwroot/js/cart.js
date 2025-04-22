// Cart functionality
// Use window object to make cart globally available
window.cart = window.cart || [];
const MIN_ORDER_AMOUNT = 100; // Minimum order amount in rupees

// Load cart from localStorage if available
function loadCart() {
    const savedCart = localStorage.getItem('allHoursCafeCart');
    if (savedCart) {
        window.cart = JSON.parse(savedCart);
    } else {
        window.cart = [];
    }
    updateCartUI();
    updateCartCount();
    updateMenuItemButtons();
}

// Save cart to localStorage
function saveCart() {
    localStorage.setItem('allHoursCafeCart', JSON.stringify(window.cart));

    // Also save to session for server-side access
    saveCartToSession();
}

// Save cart to session
async function saveCartToSession() {
    try {
        await fetch('/api/cart/save', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify(window.cart)
        });
    } catch (error) {
        console.error('Error saving cart to session:', error);
    }
}

// Add item to cart
function addToCart(id, name, price, imageUrl) {
    // Store the current state of the floating cart before updating
    const floatingCart = document.getElementById('floatingCart');
    const wasCartVisible = floatingCart && !floatingCart.classList.contains('hidden');

    // Check if item is already in cart
    const existingItem = window.cart.find(item => item.id === id);

    if (existingItem) {
        // Increment quantity if item already exists
        existingItem.quantity += 1;
    } else {
        // Add new item to cart
        window.cart.push({
            id: id,
            name: name,
            price: price,
            imageUrl: imageUrl,
            quantity: 1
        });
    }

    // Update UI and save cart
    updateCartUI();
    updateCartCount(true); // Pass true to animate the cart icon
    updateMenuItemButtons();
    updateFloatingCartItems();
    saveCart();

    // If cart was visible, keep it visible
    if (wasCartVisible) {
        if (floatingCart) {
            floatingCart.classList.remove('hidden');
            // Hide the toggle button when cart is visible
            const floatingCartToggle = document.getElementById('floatingCartToggle');
            if (floatingCartToggle) {
                floatingCartToggle.classList.add('hidden');
            }
        }
    }

    // Show notification
    showNotification(`${name} added to cart`);
}

// Remove item from cart
function removeFromCart(id) {
    const index = window.cart.findIndex(item => item.id === id);
    if (index !== -1) {
        const removedItem = window.cart[index];
        window.cart.splice(index, 1);

        // Store the current state of the floating cart before updating
        const floatingCart = document.getElementById('floatingCart');
        const wasCartVisible = floatingCart && !floatingCart.classList.contains('hidden');

        // Update UI elements
        updateCartUI();
        updateCartCount();
        updateMenuItemButtons();
        updateFloatingCartItems();
        saveCart();

        // If cart was visible and there are still items, keep it visible
        if (wasCartVisible && window.cart.length > 0) {
            if (floatingCart) {
                floatingCart.classList.remove('hidden');
                // Hide the toggle button when cart is visible
                const floatingCartToggle = document.getElementById('floatingCartToggle');
                if (floatingCartToggle) {
                    floatingCartToggle.classList.add('hidden');
                }
            }
        }

        showNotification(`${removedItem.name} removed from cart`);
    }
}

// Update item quantity
function updateQuantity(id, newQuantity) {
    const item = window.cart.find(item => item.id === id);
    if (item) {
        // Store the current state of the floating cart before updating
        const floatingCart = document.getElementById('floatingCart');
        const wasCartVisible = floatingCart && !floatingCart.classList.contains('hidden');

        if (newQuantity <= 0) {
            removeFromCart(id);
        } else {
            const oldQuantity = item.quantity;
            item.quantity = newQuantity;
            updateCartUI();

            // Animate the cart icon only if quantity increased
            updateCartCount(newQuantity > oldQuantity);
            updateMenuItemButtons();
            updateFloatingCartItems();
            saveCart();

            // If cart was visible, keep it visible
            if (wasCartVisible) {
                if (floatingCart) {
                    floatingCart.classList.remove('hidden');
                    // Hide the toggle button when cart is visible
                    const floatingCartToggle = document.getElementById('floatingCartToggle');
                    if (floatingCartToggle) {
                        floatingCartToggle.classList.add('hidden');
                    }
                }
            }
        }
    }
}

// Calculate cart total
function calculateTotal() {
    return window.cart.reduce((total, item) => total + (item.price * item.quantity), 0);
}

// Update cart UI
function updateCartUI() {
    const cartItemsElement = document.getElementById('cartItems');
    const cartTotalElement = document.getElementById('cartTotal');
    const checkoutButton = document.getElementById('checkoutButton');
    const minOrderMessage = document.getElementById('minOrderMessage');

    // If cart elements don't exist, we're using the version without the cart dropdown
    // So we don't need to update the cart UI
    if (!cartItemsElement) return;

    // Clear current cart items
    cartItemsElement.innerHTML = '';

    if (window.cart.length === 0) {
        // Cart is empty
        cartItemsElement.innerHTML = '<p class="empty-cart-message">Your cart is empty</p>';
        if (cartTotalElement) cartTotalElement.textContent = '₹0.00';
        if (checkoutButton) {
            checkoutButton.style.display = 'none'; // Hide the checkout link
            checkoutButton.classList.remove('active');
        }
        if (minOrderMessage) minOrderMessage.textContent = 'Add items to your cart to proceed';
    } else {
        // Add each item to the cart UI
        window.cart.forEach(item => {
            const itemElement = document.createElement('div');
            itemElement.className = 'cart-item';
            itemElement.innerHTML = `
                <div class="cart-item-details">
                    <h3>${item.name}</h3>
                    <div class="cart-item-price">₹${(item.price * item.quantity).toFixed(2)}</div>
                </div>
                <div class="cart-item-quantity">
                    <button onclick="updateQuantity(${item.id}, ${item.quantity - 1})" class="quantity-btn">-</button>
                    <span>${item.quantity}</span>
                    <button onclick="updateQuantity(${item.id}, ${item.quantity + 1})" class="quantity-btn">+</button>
                </div>
                <button onclick="removeFromCart(${item.id})" class="remove-item-btn">×</button>
            `;
            cartItemsElement.appendChild(itemElement);
        });

        // Update total and checkout button
        const total = calculateTotal();
        if (cartTotalElement) cartTotalElement.textContent = `₹${total.toFixed(2)}`;

        // Update checkout button and message
        if (checkoutButton && minOrderMessage) {
            if (total >= MIN_ORDER_AMOUNT) {
                // Enable checkout
                checkoutButton.style.display = 'block'; // Show the checkout link
                checkoutButton.classList.add('active');
                minOrderMessage.textContent = '';
                console.log('Checkout enabled, link shown');
            } else {
                // Disable checkout
                checkoutButton.style.display = 'none'; // Hide the checkout link
                checkoutButton.classList.remove('active');
                const remaining = MIN_ORDER_AMOUNT - total;
                minOrderMessage.textContent = `Add ₹${remaining.toFixed(2)} more to meet minimum order amount`;
                console.log('Checkout disabled, link hidden');
            }
        }
    }
}

// Update cart count in the header and floating checkout button
function updateCartCount(animate = false) {
    const cartCountElement = document.getElementById('cartCount');
    const cartLinkElement = document.querySelector('.cart-link');
    const floatingCheckout = document.getElementById('floatingCheckout');
    const floatingCartCount = document.getElementById('floatingCartCount');
    const floatingCartTotal = document.getElementById('floatingCartTotal');

    // Calculate total items and cart total
    const itemCount = window.cart.reduce((count, item) => count + item.quantity, 0);
    const cartTotal = calculateTotal();

    // Update header cart count if it exists
    if (cartCountElement) {
        cartCountElement.textContent = itemCount;
        cartCountElement.style.display = 'flex';

        // Add animation if requested and there are items in the cart
        if (itemCount > 0 && animate && cartLinkElement) {
            // Remove any existing animation classes
            cartLinkElement.classList.remove('cart-bounce', 'cart-pulse');

            // Force a reflow to restart the animation
            void cartLinkElement.offsetWidth;

            // Add the bounce animation class
            cartLinkElement.classList.add('cart-bounce');

            // Add pulse animation to the count
            cartCountElement.classList.remove('cart-pulse');
            void cartCountElement.offsetWidth;
            cartCountElement.classList.add('cart-pulse');

            // Remove the animation classes after they complete
            setTimeout(() => {
                cartLinkElement.classList.remove('cart-bounce');
                cartCountElement.classList.remove('cart-pulse');
            }, 1000);
        }
    }

    // Update floating cart if it exists
    const floatingCart = document.getElementById('floatingCart');
    const floatingCartItems = document.getElementById('floatingCartItems');
    const floatingCartToggle = document.getElementById('floatingCartToggle');

    // Store the current state of the floating cart
    const isCartVisible = floatingCart && !floatingCart.classList.contains('hidden');

    if (floatingCartItems) {
        if (itemCount > 0) {
            // Update cart count and total
            if (floatingCartCount) floatingCartCount.textContent = itemCount;
            if (floatingCartTotal) floatingCartTotal.textContent = `₹${cartTotal.toFixed(2)}`;

            // Update floating cart items
            updateFloatingCartItems();

            // If the cart is visible, keep it visible and hide the toggle
            if (isCartVisible) {
                if (floatingCart) floatingCart.classList.remove('hidden');
                if (floatingCartToggle) floatingCartToggle.classList.add('hidden');
            }
            // Otherwise, show the toggle button
            else if (floatingCartToggle) {
                floatingCartToggle.classList.remove('hidden');

                // Animate the toggle button if requested
                if (animate) {
                    floatingCartToggle.classList.remove('animate');
                    void floatingCartToggle.offsetWidth;
                    floatingCartToggle.classList.add('animate');

                    // Remove animation class after it completes
                    setTimeout(() => {
                        floatingCartToggle.classList.remove('animate');
                    }, 500);
                }
            }
        } else {
            // Hide both the cart and toggle button when cart is empty
            if (floatingCart) floatingCart.classList.add('hidden');
            if (floatingCartToggle) floatingCartToggle.classList.add('hidden');
        }
    }
}

// Show notification
function showNotification(message) {
    // Check if a notification already exists
    let notification = document.querySelector('.notification');

    // If not, create a new one
    if (!notification) {
        notification = document.createElement('div');
        notification.className = 'notification';
        document.body.appendChild(notification);
    }

    // Update the message
    notification.textContent = message;
    notification.classList.remove('fade-out');

    // Make sure the notification is visible and not covered by other elements
    notification.style.zIndex = '1001';

    // Remove notification after 3 seconds
    clearTimeout(window.notificationTimeout);
    window.notificationTimeout = setTimeout(() => {
        notification.classList.add('fade-out');
        setTimeout(() => {
            if (notification.parentNode) {
                document.body.removeChild(notification);
            }
        }, 500);
    }, 2500);
}

// Get item quantity in cart
function getItemQuantityInCart(id) {
    const item = window.cart.find(item => item.id === id);
    return item ? item.quantity : 0;
}

// Update floating cart items
function updateFloatingCartItems() {
    const floatingCartItems = document.getElementById('floatingCartItems');
    if (!floatingCartItems) return;

    // Store the current state of the floating cart
    const floatingCart = document.getElementById('floatingCart');
    const wasCartVisible = floatingCart && !floatingCart.classList.contains('hidden');

    // Clear current cart items
    floatingCartItems.innerHTML = '';

    if (window.cart.length === 0) {
        // Cart is empty
        floatingCartItems.innerHTML = '<p class="empty-cart-message">Your cart is empty</p>';

        // Hide the cart if it was visible
        if (wasCartVisible && floatingCart) {
            setTimeout(() => {
                floatingCart.classList.add('hidden');
                // Hide the toggle button too since cart is empty
                const floatingCartToggle = document.getElementById('floatingCartToggle');
                if (floatingCartToggle) {
                    floatingCartToggle.classList.add('hidden');
                }
            }, 500); // Small delay to show the empty message before hiding
        }
    } else {
        // Add each item to the floating cart UI
        window.cart.forEach(item => {
            const itemElement = document.createElement('div');
            itemElement.className = 'floating-cart-item';
            itemElement.innerHTML = `
                <div class="floating-cart-item-details">
                    <div class="floating-cart-item-name">${item.name}</div>
                    <div class="floating-cart-item-price">₹${(item.price * item.quantity).toFixed(2)}</div>
                </div>
                <div class="floating-cart-item-quantity">
                    <button onclick="updateQuantity(${item.id}, ${item.quantity - 1})" class="quantity-btn">-</button>
                    <span>${item.quantity}</span>
                    <button onclick="updateQuantity(${item.id}, ${item.quantity + 1})" class="quantity-btn">+</button>
                </div>
                <button onclick="removeFromCart(${item.id})" class="remove-item-btn">×</button>
            `;

            // Add event listeners to prevent event propagation
            itemElement.querySelectorAll('button').forEach(button => {
                button.addEventListener('click', function(e) {
                    e.stopPropagation();
                });
            });

            floatingCartItems.appendChild(itemElement);
        });

        // If cart was visible, ensure it stays visible
        if (wasCartVisible && floatingCart) {
            floatingCart.classList.remove('hidden');
            // Hide the toggle button when cart is visible
            const floatingCartToggle = document.getElementById('floatingCartToggle');
            if (floatingCartToggle) {
                floatingCartToggle.classList.add('hidden');
            }
        }
    }
}

// Update menu item buttons based on cart contents
function updateMenuItemButtons() {
    // Get all item order controls
    const itemControls = document.querySelectorAll('.item-order-controls');

    // Loop through each control
    itemControls.forEach(control => {
        const itemId = parseInt(control.getAttribute('data-item-id'));
        const orderButton = control.querySelector('.order-button');
        const quantityControl = control.querySelector('.quantity-control');
        const quantityDisplay = control.querySelector('.quantity-display');

        // Check if item is in cart
        const quantity = getItemQuantityInCart(itemId);

        if (quantity > 0) {
            // Item is in cart, show quantity control
            if (orderButton) orderButton.style.display = 'none';
            if (quantityControl) {
                quantityControl.style.display = 'flex';
                if (quantityDisplay) quantityDisplay.textContent = quantity;
            }
        } else {
            // Item is not in cart, show order button
            if (orderButton) orderButton.style.display = 'block';
            if (quantityControl) quantityControl.style.display = 'none';
        }
    });
}

// Change checkout quantity using DOM value
function changeCheckoutQty(id, delta) {
    const qtySpan = document.getElementById('checkout-qty-' + id);
    let currentQty = parseInt(qtySpan.textContent, 10) || 1;
    const newQty = currentQty + delta;
    updateCheckoutQuantity(id, newQty);
}

// Update quantity from checkout page
function updateCheckoutQuantity(id, newQuantity) {
    if (newQuantity <= 0) {
        removeFromCart(id);
        // Remove the item from the checkout UI
        const orderItem = document.querySelector('.order-item[data-item-id="' + id + '"]');
        if (orderItem) orderItem.remove();
    } else {
        const item = window.cart.find(item => item.id === id);
        if (item) {
            item.quantity = newQuantity;
            saveCart();
            // Update the quantity display in the checkout summary
            const qtyDisplay = document.getElementById('checkout-qty-' + id);
            if (qtyDisplay) qtyDisplay.textContent = newQuantity;
            // Optionally update item total price in UI
            const itemTotal = document.querySelector('.order-item[data-item-id="' + id + '"] .item-total');
            if (itemTotal) itemTotal.textContent = '₹' + (item.price * newQuantity).toFixed(2);
        }
    }
    // Update order totals
    updateCheckoutTotals();
}

// Update checkout totals (subtotal, tax, delivery, total)
function updateCheckoutTotals() {
    let subtotal = 0;
    window.cart.forEach(item => {
        subtotal += item.price * item.quantity;
    });
    const tax = +(subtotal * 0.05).toFixed(2);
    const delivery = 30; // Hardcoded, update if needed
    const total = +(subtotal + tax + delivery).toFixed(2);
    const subtotalEl = document.querySelector('.order-totals .total-row span:nth-child(2)');
    const taxEl = document.querySelector('.order-totals .total-row:nth-child(2) span:nth-child(2)');
    const deliveryEl = document.querySelector('.order-totals .delivery-fee span:nth-child(2)');
    const totalEl = document.querySelector('.order-totals .grand-total span:nth-child(2)');
    if (subtotalEl) subtotalEl.textContent = '₹' + subtotal.toFixed(2);
    if (taxEl) taxEl.textContent = '₹' + tax.toFixed(2);
    if (deliveryEl) deliveryEl.textContent = '₹' + delivery.toFixed(2);
    if (totalEl) totalEl.textContent = '₹' + total.toFixed(2);
}

// Initialize cart
document.addEventListener('DOMContentLoaded', function() {
    console.log('Cart.js: Initializing cart');

    // Always initialize the cart
    loadCart();

    // Force update the cart count
    updateCartCount();

    // Update menu item buttons
    updateMenuItemButtons();

    // Update floating cart items
    updateFloatingCartItems();

    // Log the cart state
    console.log('Cart initialized with', window.cart.length, 'items');

    // Cart toggle has been removed

    // The checkout button is now an <a> tag that links directly to /Checkout
    // No need to add an event listener
    console.log('Cart.js: Checkout button is now a direct link to /Checkout');
});
