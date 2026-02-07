/**
 * Cedeva Choices.js Helper
 * Provides reusable functions for initializing Choices.js with Cedeva styling
 */

const CedevaChoices = {
    /**
     * Default configuration for Choices.js
     */
    defaultConfig: {
        searchEnabled: false,
        itemSelectText: '',
        shouldSort: false,
        removeItemButton: false,
        allowHTML: false
    },

    /**
     * Initialize Choices.js on a single select element
     * @param {string|HTMLElement} selector - CSS selector or DOM element
     * @param {Object} options - Optional Choices.js configuration overrides
     * @param {Function} onChange - Optional callback function on change
     * @returns {Choices|null} - The Choices instance or null if element not found
     */
    init: function(selector, options = {}, onChange = null) {
        const element = typeof selector === 'string'
            ? document.querySelector(selector)
            : selector;

        if (!element) {
            console.warn('Choices.js: Element not found:', selector);
            return null;
        }

        const config = { ...this.defaultConfig, ...options };
        const choices = new Choices(element, config);

        // Add change event listener if provided
        if (onChange && typeof onChange === 'function') {
            element.addEventListener('change', onChange);
        }

        return choices;
    },

    /**
     * Initialize Choices.js on multiple select elements
     * @param {string} selector - CSS selector for multiple elements
     * @param {Object} options - Optional Choices.js configuration overrides
     * @returns {Array} - Array of Choices instances
     */
    initAll: function(selector, options = {}) {
        const elements = document.querySelectorAll(selector);
        const instances = [];

        elements.forEach(element => {
            const instance = this.init(element, options);
            if (instance) {
                instances.push(instance);
            }
        });

        return instances;
    },

    /**
     * Initialize with search enabled (for long lists)
     * @param {string|HTMLElement} selector - CSS selector or DOM element
     * @param {Object} options - Optional configuration overrides
     * @returns {Choices|null}
     */
    initWithSearch: function(selector, options = {}) {
        return this.init(selector, { ...options, searchEnabled: true });
    },

    /**
     * Initialize with auto-submit on change
     * @param {string|HTMLElement} selector - CSS selector or DOM element
     * @param {Object} options - Optional configuration overrides
     * @returns {Choices|null}
     */
    initWithAutoSubmit: function(selector, options = {}) {
        const element = typeof selector === 'string'
            ? document.querySelector(selector)
            : selector;

        if (!element || !element.form) {
            console.warn('Choices.js: Element not found or not in a form:', selector);
            return null;
        }

        return this.init(selector, options, function() {
            element.form.submit();
        });
    },

    /**
     * Initialize all .form-select elements on the page
     * @param {Object} options - Optional configuration overrides
     * @returns {Array} - Array of Choices instances
     */
    initAllSelects: function(options = {}) {
        return this.initAll('.form-select:not(.no-choices)', options);
    },

    /**
     * Destroy a Choices instance
     * @param {Choices} instance - The Choices instance to destroy
     */
    destroy: function(instance) {
        if (instance && typeof instance.destroy === 'function') {
            instance.destroy();
        }
    }
};

// Export for use in other scripts
if (typeof module !== 'undefined' && module.exports) {
    module.exports = CedevaChoices;
}
