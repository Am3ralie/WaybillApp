// ─── Автоматически скрывать flash-уведомления ────────────────────────────────
document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('.alert').forEach(el => {
        setTimeout(() => {
            el.style.transition = 'opacity .4s';
            el.style.opacity = '0';
            setTimeout(() => el.remove(), 400);
        }, 4000);
    });

    // ─── Подтверждение удаления (резервный вариант через data-confirm) ────────
    document.querySelectorAll('[data-confirm]').forEach(btn => {
        btn.addEventListener('click', e => {
            if (!confirm(btn.dataset.confirm)) e.preventDefault();
        });
    });

    // ─── Подсветка активной строки таблицы ───────────────────────────────────
    document.querySelectorAll('tbody tr').forEach(tr => {
        tr.style.cursor = 'default';
    });

    // ─── «Печать страницы» — кнопка без перехода ─────────────────────────────
    document.querySelectorAll('[data-print]').forEach(btn => {
        btn.addEventListener('click', () => window.print());
    });
});

// ─── Расчёт топлива (используется в Create/Edit путевого листа) ──────────────
// Функции calcMileage() и calcFuel() определены прямо во View,
// так как они зависят от Razor-констант (коэффициентов нормы).
// Здесь только вспомогательная утилита форматирования.

function fmt(n, decimals = 2) {
    if (n === null || n === undefined || isNaN(n)) return '—';
    return n.toLocaleString('ru-RU', {
        minimumFractionDigits: decimals,
        maximumFractionDigits: decimals
    });
}
