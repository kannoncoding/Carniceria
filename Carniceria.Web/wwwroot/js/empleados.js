(() => {
    "use strict";

    // Mantiene los puestos disponibles sincronizados con el area seleccionada.
    function inicializarPuestosEmpleado() {
        const campoArea = document.getElementById("AreaTrabajo");
        const campoPuesto = document.getElementById("Puesto");
        const elementoCatalogo = document.getElementById(
            "catalogo-puestos-empleado"
        );

        if (!campoArea || !campoPuesto || !elementoCatalogo) {
            return;
        }

        // Catalogo serializado por la vista para filtrar puestos por area.
        let puestosPorArea;

        try {
            puestosPorArea = JSON.parse(elementoCatalogo.textContent);
        } catch (error) {
            console.error(
                "No se pudo leer el catálogo de puestos del empleado.",
                error
            );

            return;
        }

        function actualizarPuestos() {
            // Conserva el puesto solo si sigue disponible para el area actual.
            const puestoAnterior = campoPuesto.value;
            const puestosDelArea = puestosPorArea[campoArea.value];

            const puestosPermitidos = Array.isArray(puestosDelArea)
                ? puestosDelArea
                : [];

            const textoInicial = puestosPermitidos.length > 0
                ? "Seleccione un puesto"
                : "Seleccione primero un área de trabajo";

            // Construye las opciones como texto, sin insertar marcado.
            const opciones = document.createDocumentFragment();

            const opcionInicial = document.createElement("option");
            opcionInicial.value = "";
            opcionInicial.textContent = textoInicial;
            opciones.appendChild(opcionInicial);

            puestosPermitidos.forEach((puesto) => {
                const opcion = document.createElement("option");
                opcion.value = puesto;
                opcion.textContent = puesto;
                opciones.appendChild(opcion);
            });

            campoPuesto.replaceChildren(opciones);

            campoPuesto.value = puestosPermitidos.includes(puestoAnterior)
                ? puestoAnterior
                : "";

            campoPuesto.disabled = puestosPermitidos.length === 0;
        }

        // El evento change actualiza los puestos disponibles.
        campoArea.addEventListener("change", actualizarPuestos);

        // El evento pageshow sincroniza el formulario restaurado.
        window.addEventListener("pageshow", actualizarPuestos);

        // La seleccion inicial conserva el valor renderizado por el servidor.
        actualizarPuestos();
    }

    if (document.readyState === "loading") {
        document.addEventListener(
            "DOMContentLoaded",
            inicializarPuestosEmpleado,
            { once: true }
        );
    } else {
        inicializarPuestosEmpleado();
    }
})();
