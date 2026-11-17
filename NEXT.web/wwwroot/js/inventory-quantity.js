"use strict";

(() => {
    function showField(form, name, visible, required = false) {
        const wrapper = form.querySelector(`[data-field="${name}"]`);
        if (!wrapper) return;
        wrapper.hidden = !visible;
        const field = form.elements.namedItem(name);
        field.disabled = !visible;
        field.required = visible && required;
    }

    function preview(form, output, quantity, baseUnit, multiplier) {
        output.textContent = "—";
        if (!Number.isFinite(quantity) || quantity <= 0 || !Number.isFinite(multiplier)) return;
        const amount = new Intl.NumberFormat(document.documentElement.lang || "en", {
            maximumFractionDigits: 3
        }).format(quantity * multiplier);
        if (!baseUnit) {
            output.textContent = amount;
        } else {
            output.textContent = form.dataset.baseQuantityFormat.replace(/\{([01])\}/g,
                (_, index) => index === "0" ? amount : baseUnit);
        }
    }

    function selectUnits(unit, baseUnit, packagingUnit, capacity) {
        const baseOption = unit.querySelector('option[value="false"]');
        const packageOption = unit.querySelector('option[value="true"]');
        baseOption.dataset.label ??= baseOption.textContent;
        baseOption.textContent = baseUnit || baseOption.dataset.label;
        packageOption.disabled = !packagingUnit || !Number.isInteger(capacity) || capacity <= 0;
        packageOption.hidden = packageOption.disabled;
        if (!packageOption.disabled) packageOption.textContent = `${packagingUnit} (${capacity})`;
        if (unit.value === "" || packageOption.disabled && unit.value === "true") unit.value = "false";
    }

    function update(form) {
        const field = name => form.elements.namedItem(name);
        const receipt = form.querySelector("#field-ReceiptQuantityPreview");
        if (receipt) {
            const product = field("ProductId").selectedOptions[0];
            const unit = field("ReceiveInPackages");
            const capacity = Number(product?.dataset.unitsPerPackage);
            selectUnits(unit, product?.dataset.baseUnit, product?.dataset.packagingUnit, capacity);
            const quantity = product?.value ? field("Quantity").valueAsNumber : NaN;
            preview(form, receipt, quantity, product?.dataset.baseUnit, unit.value === "true" ? capacity : 1);
        }
        const packaging = field("PackagingUnitId");
        if (packaging) {
            showField(form, "UnitsPerPackage", Boolean(packaging.value), true);
            if (!packaging.value) field("UnitsPerPackage").value = "";
        }
        const opening = form.querySelector("#field-OpeningQuantityPreview");
        if (opening) {
            const enabled = form.querySelector('input[type="checkbox"][name="AddOpeningStock"]').checked;
            const expiry = enabled && form.querySelector('input[type="checkbox"][name="HasExpiry"]').checked;
            const unit = field("OpeningInPackages");
            const base = field("UnitOfMeasureId").selectedOptions[0];
            const pack = packaging.selectedOptions[0];
            const capacity = field("UnitsPerPackage").valueAsNumber;
            showField(form, "OpeningWarehouseId", enabled, true);
            showField(form, "OpeningInPackages", enabled);
            showField(form, "OpeningQuantity", enabled, true);
            showField(form, "OpeningBatchNumber", expiry, true);
            showField(form, "OpeningExpiryDate", expiry, true);
            form.querySelector('[data-field="OpeningQuantityPreview"]').hidden = !enabled;
            selectUnits(unit, base?.value && base.textContent, pack?.value && pack.textContent, capacity);
            preview(form, opening, field("OpeningQuantity").valueAsNumber, base?.value && base.textContent,
                unit.value === "true" ? capacity : 1);
        }
    }

    function initialize() {
        document.querySelectorAll("[data-modal-form]").forEach(update);
    }

    function onEdit(event) {
        const form = event.target.closest("[data-modal-form]");
        if (form) update(form);
    }

    document.addEventListener("input", onEdit);
    document.addEventListener("change", onEdit);
    document.addEventListener("next:formloaded", initialize);
    document.addEventListener("next:navigated", initialize);
})();
