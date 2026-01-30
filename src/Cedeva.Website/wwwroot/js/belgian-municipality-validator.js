// Client-side validation for postal code + city combination
$.validator.addMethod("belgianmunicipality", function (value, element, params) {
    var cityFieldId = $(element).attr('data-val-belgianmunicipality-cityfieldid');
    var postalCodeFieldId = $(element).attr('data-val-belgianmunicipality-postalcodefieldid');

    var city = $("#" + cityFieldId).val();
    var postalCode = $("#" + postalCodeFieldId).val();

    // If both fields are empty, no error
    if (!city && !postalCode) {
        return true;
    }

    // If only one is filled, error
    if (!city || !postalCode) {
        return false;
    }

    // Validation via AJAX
    var isValid = true;
    $.ajax({
        url: "/api/AddressApi/validate-municipality",
        type: "GET",
        async: false,
        data: {
            city: city,
            postalCode: postalCode
        },
        success: function (result) {
            isValid = result.isValid === true;
        },
        error: function () {
            isValid = false;
        }
    });

    return isValid;
});

$.validator.unobtrusive.adapters.add("belgianmunicipality", ["cityfieldid", "postalcodefieldid"], function (options) {
    options.rules["belgianmunicipality"] = {
        cityfieldid: options.params.cityfieldid,
        postalcodefieldid: options.params.postalcodefieldid
    };
    options.messages["belgianmunicipality"] = options.message;
});
