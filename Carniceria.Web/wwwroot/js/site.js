(() => {
    "use strict";

    function inicializarResumenesFechas() {
        const camposFecha = document.querySelectorAll(
            'input[type="date"][data-fecha-resumen]'
        );

        camposFecha.forEach((campo) => {
            const resumenId = campo.dataset.fechaResumen;

            if (!resumenId) {
                return;
            }

            const resumen = document.getElementById(resumenId);

            if (!resumen) {
                return;
            }

            function actualizarResumen() {
                const partes = /^(\d{4})-(\d{2})-(\d{2})$/.exec(
                    campo.value
                );

                if (!partes) {
                    resumen.textContent =
                        "Fecha seleccionada: sin seleccionar.";

                    return;
                }

                const anio = partes[1];
                const mes = partes[2];
                const dia = partes[3];

                // Reordenar el texto sin convertirlo a una fecha con zona horaria.
                resumen.textContent =
                    `Fecha seleccionada: ${dia}/${mes}/${anio}.`;
            }

            campo.addEventListener("input", actualizarResumen);
            campo.addEventListener("change", actualizarResumen);

            // Actualizar también al restaurar una página con Atrás/Adelante.
            window.addEventListener("pageshow", actualizarResumen);

            actualizarResumen();
        });
    }

    if (document.readyState === "loading") {
        document.addEventListener(
            "DOMContentLoaded",
            inicializarResumenesFechas,
            { once: true }
        );
    } else {
        inicializarResumenesFechas();
    }
})();