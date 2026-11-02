// Oculta resultados anteriores cuando cambian los datos del formulario.
document.addEventListener("input", limpiarCalculoAnterior);
document.addEventListener("change", limpiarCalculoAnterior);

// Quita importes previos cuando cambian los datos que los originaron.
function limpiarCalculoAnterior(evento) {
    if (!(evento.target instanceof Element)) {
        return;
    }

    const formulario = evento.target.closest("#formulario-factura");
    const nombreCampo = evento.target.getAttribute("name") ?? "";
    if (!formulario || nombreCampo === "Detalles.Index" ||
        nombreCampo.endsWith(".ClaveFormulario") ||
        nombreCampo.endsWith(".Id")) {
        return;
    }

    formulario.querySelector("#resumen-calculo")?.remove();
    formulario.querySelectorAll(".valores-linea").forEach((elemento) => elemento.remove());
}
