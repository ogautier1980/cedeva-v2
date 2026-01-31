// Unified address autocomplete (PostalCode + City in one field)
function initializeUnifiedAddressAutocomplete(combinedInputId, postalCodeHiddenId, cityHiddenId, apiUrl) {
    if (!apiUrl) {
        apiUrl = "/api/AddressApi/municipalities/search";
    }

    var combinedInput = $(combinedInputId);
    var postalCodeHidden = $(postalCodeHiddenId);
    var cityHidden = $(cityHiddenId);

    // Initialize autocomplete on the combined field
    combinedInput.autocomplete({
        source: function (request, response) {
            $.ajax({
                url: apiUrl,
                data: { term: request.term },
                dataType: "json",
                success: function (data) {
                    // Transform data to show "PostalCode City" format
                    var transformedData = data.map(function(item) {
                        return {
                            label: item.postalCode + ' ' + item.value,  // "5030 Gembloux"
                            value: item.postalCode + ' ' + item.value,  // What gets filled in the input
                            postalCode: item.postalCode,
                            city: item.value
                        };
                    });
                    response(transformedData);
                },
                error: function () {
                    response([]);
                }
            });
        },
        minLength: 2,
        select: function (event, ui) {
            // Fill the combined input with "PostalCode City"
            combinedInput.val(ui.item.value);
            // Fill hidden fields
            postalCodeHidden.val(ui.item.postalCode);
            cityHidden.val(ui.item.city);

            // Trigger validation
            triggerValidation();
            return false;
        }
    });

    // Parse manually entered text (e.g., "5030 Gembloux" or "5030" or "Gembloux")
    function parseAndValidate() {
        var value = combinedInput.val().trim();

        if (!value) {
            postalCodeHidden.val('');
            cityHidden.val('');
            return;
        }

        // Try to parse "PostalCode City" format
        var parts = value.split(/\s+/);

        if (parts.length >= 2 && /^\d+$/.test(parts[0])) {
            // First part is numeric (postal code)
            postalCodeHidden.val(parts[0]);
            cityHidden.val(parts.slice(1).join(' '));
        } else if (parts.length === 1 && /^\d+$/.test(parts[0])) {
            // Only postal code entered
            postalCodeHidden.val(parts[0]);
            cityHidden.val('');
        } else {
            // Only city name entered (or invalid format)
            postalCodeHidden.val('');
            cityHidden.val(value);
        }

        triggerValidation();
    }

    function triggerValidation() {
        clearTimeout(combinedInput.data('validationDelay'));
        combinedInput.data('validationDelay', setTimeout(function() {
            combinedInput.valid();
        }, 150));
    }

    // Attach event handlers
    combinedInput.on('blur', parseAndValidate);
    combinedInput.on('change', parseAndValidate);
}
