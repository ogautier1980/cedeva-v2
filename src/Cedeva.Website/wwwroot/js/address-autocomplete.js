// Address autocomplete initialization function
function initializeAddressAutocomplete(cityInputId, postalCodeInputId, apiUrl) {
    if (!apiUrl) {
        apiUrl = "/api/AddressApi/municipalities/search";
    }

    var cityInput = $(cityInputId);
    var postalCodeInput = $(postalCodeInputId);

    // Validation is attached to the CityInput field
    // We need to trigger CityInput validation when either field changes
    function triggerCityValidationWithDelay() {
        clearTimeout(cityInput.data('validationDelay'));
        cityInput.data('validationDelay', setTimeout(function() {
            // Calls .valid() on CityInput, which triggers the 'belgianmunicipality' rule
            // This rule uses the current values of both fields for validation
            cityInput.valid();
        }, 150)); // Small delay to allow field value to update
    }

    // Autocomplete for city
    cityInput.autocomplete({
        source: function (request, response) {
            $.ajax({
                url: apiUrl,
                data: { term: request.term },
                dataType: "json",
                success: function (data) {
                    response(data);
                },
                error: function () {
                    response([]);
                }
            });
        },
        minLength: 3,
        select: function (event, ui) {
            cityInput.val(ui.item.value);
            postalCodeInput.val(ui.item.postalCode);
            triggerCityValidationWithDelay();
            return false;
        }
    });

    // Autocomplete for postal code
    postalCodeInput.autocomplete({
        source: function (request, response) {
            $.ajax({
                url: apiUrl,
                data: { term: request.term },
                dataType: "json",
                success: function (data) {
                    response(data);
                },
                error: function () {
                    response([]);
                }
            });
        },
        minLength: 3,
        select: function (event, ui) {
            cityInput.val(ui.item.value);
            postalCodeInput.val(ui.item.postalCode);
            triggerCityValidationWithDelay();
            return false;
        }
    });

    // Trigger validation when user leaves field (blur)
    cityInput.on('blur', triggerCityValidationWithDelay);
    postalCodeInput.on('blur', triggerCityValidationWithDelay);

    // Trigger validation when user manually modifies field (change)
    cityInput.on('change', triggerCityValidationWithDelay);
    postalCodeInput.on('change', triggerCityValidationWithDelay);

    // Optional: trigger validation during typing (input), with delay
    cityInput.on('input', triggerCityValidationWithDelay);
    postalCodeInput.on('input', triggerCityValidationWithDelay);
}
