const state = {
  products: [],
  suppliers: [],
  dashboard: null,
  stock: [],
  receipts: [],
  writeOffs: [],
  profit: null,
  expired: [],
  soon: [],
  reportPeriod: null
};

const reasonLabels = {
  Sale: "Продажа",
  Expiry: "Срок годности",
  Damage: "Порча"
};

const reasonClasses = {
  Sale: "sale",
  Expiry: "expiry",
  Damage: "damage"
};

const numberFormat = new Intl.NumberFormat("ru-RU", { maximumFractionDigits: 2 });
const quantityFormat = new Intl.NumberFormat("ru-RU", { maximumFractionDigits: 3 });
const moneyFormat = new Intl.NumberFormat("ru-RU", {
  style: "currency",
  currency: "RUB",
  minimumFractionDigits: 2,
  maximumFractionDigits: 2
});

const $ = (id) => document.getElementById(id);

function money(value) {
  return moneyFormat.format(Number(value || 0));
}

function qty(value) {
  return quantityFormat.format(Number(value || 0));
}

function qtyWithUnit(value, unit) {
  return `${qty(value)} ${unit || "ед."}`;
}

function esc(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}

function isoDate(date) {
  const copy = new Date(date.getTime() - date.getTimezoneOffset() * 60000);
  return copy.toISOString().slice(0, 10);
}

function addDays(date, days) {
  const copy = new Date(date);
  copy.setDate(copy.getDate() + days);
  return copy;
}

function parseDate(value) {
  return value ? new Date(`${value}T00:00:00`) : null;
}

function formatDate(value) {
  const date = typeof value === "string" ? parseDate(value) : value;
  return date
    ? date.toLocaleDateString("ru-RU", { day: "2-digit", month: "2-digit", year: "numeric" })
    : "—";
}

function formatDateShort(value) {
  const date = parseDate(value);
  return date ? date.toLocaleDateString("ru-RU", { day: "2-digit", month: "2-digit" }) : "—";
}

function daysLabel(days) {
  if (days === 0) return "сегодня";
  if (days === 1) return "завтра";
  return `${days} дн.`;
}

function plural(count, forms) {
  const value = Math.abs(count) % 100;
  const last = value % 10;
  if (value > 10 && value < 20) return forms[2];
  if (last > 1 && last < 5) return forms[1];
  if (last === 1) return forms[0];
  return forms[2];
}

async function api(path, options = {}) {
  const response = await fetch(path, {
    ...options,
    headers: {
      "Content-Type": "application/json",
      ...(options.headers || {})
    }
  });

  const text = await response.text();
  let payload = null;

  if (text) {
    try {
      payload = JSON.parse(text);
    } catch {
      payload = text;
    }
  }

  if (!response.ok) {
    const message = payload && payload.error ? payload.error : `Ошибка запроса (${response.status})`;
    throw new Error(message);
  }

  return payload;
}

let toastTimer = null;

function toast(message, isError = false) {
  const element = $("toast");
  element.textContent = message;
  element.classList.toggle("error", isError);
  element.classList.add("show");

  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => element.classList.remove("show"), 4200);
}

function setText(id, value) {
  const element = $(id);
  if (element) element.textContent = value;
}

function emptyRow(columns, text = "Нет данных") {
  return `<tr><td colspan="${columns}" class="empty">${esc(text)}</td></tr>`;
}

/* ---------- Навигация ---------- */

const viewMeta = {
  dashboard: ["Обзор склада", "Ключевые показатели магазина на сегодня"],
  stock: ["Остатки товара", "Сводный учёт партий и закупочной стоимости"],
  receipts: ["Поступления", "Приёмка товара от поставщиков"],
  writeoffs: ["Списания", "Продажи, порча и утилизация просроченного товара"],
  reports: ["Отчёты", "Прибыль за период, сроки годности и потери"]
};

function showView(name) {
  document.querySelectorAll(".nav-item").forEach((button) => {
    button.classList.toggle("active", button.dataset.view === name);
  });
  document.querySelectorAll(".view").forEach((view) => {
    view.classList.toggle("active", view.id === `view-${name}`);
  });

  const meta = viewMeta[name];
  if (meta) {
    setText("pageTitle", meta[0]);
    setText("pageSubtitle", meta[1]);
  }

  if (name === "stock") loadStock();
  if (name === "receipts") loadReceipts();
  if (name === "writeoffs") loadWriteOffs();
  if (name === "reports") loadReports();
}

/* ---------- Справочники ---------- */

async function loadReference() {
  const [suppliers, products] = await Promise.all([
    api("/api/suppliers"),
    api("/api/products")
  ]);

  state.suppliers = suppliers;
  state.products = products;

  fillSelect($("receipt-supplier"), suppliers, (item) => item.id, (item) => item.name);
  fillSelect($("receipt-product"), products, (item) => item.id, (item) => `${item.name} · ${item.sku}`);
  fillSelect($("wo-product"), products, (item) => item.id, (item) => `${item.name} · остаток ${qty(item.stockQuantity)} ${item.unit}`);
}

function fillSelect(select, items, valueSelector, labelSelector) {
  const previous = select.value;
  select.innerHTML = items
    .map((item) => `<option value="${esc(valueSelector(item))}">${esc(labelSelector(item))}</option>`)
    .join("");

  if (previous && items.some((item) => String(valueSelector(item)) === previous)) {
    select.value = previous;
  }
}

/* ---------- Обзор ---------- */

async function loadDashboard() {
  const today = new Date();
  const from = isoDate(addDays(today, -29));
  const to = isoDate(today);

  const [dashboard, profit, soon, operations] = await Promise.all([
    api("/api/reports/dashboard"),
    api(`/api/reports/profit?from=${from}&to=${to}`),
    api("/api/reports/soon-to-expire?days=7"),
    api("/api/write-offs?limit=6")
  ]);

  state.dashboard = dashboard;
  state.profit = profit;
  state.soon = soon;

  setText("stat-stock-units", `${qty(dashboard.stockUnits)} ед.`);
  setText("stat-stock-sub", `${dashboard.productsInStock} ${plural(dashboard.productsInStock, ["позиция", "позиции", "позиций"])} в наличии`);
  setText("stat-stock-value", money(dashboard.stockValue));
  setText("stat-soon", `${dashboard.soonToExpireCount} ${plural(dashboard.soonToExpireCount, ["позиция", "позиции", "позиций"])}`);
  setText("stat-soon-sub", `потери по сроку: ${money(dashboard.expiredLoss)}`);
  setText("stat-profit", money(dashboard.netProfit30d));
  setText("stat-profit-sub", `налог ${(dashboard.taxRate * 100).toFixed(1).replace(".0", "")}% · выручка ${money(dashboard.revenue30d)}`);

  const profitRows = profit.lines.slice(0, 6);
  $("dash-profit-body").innerHTML = profitRows.length
    ? profitRows.map((line) => `
      <tr>
        <td>
          <div class="cell-title">${esc(line.name)}</div>
          <div class="cell-sub">${esc(line.sku)}</div>
        </td>
        <td class="num">${esc(qtyWithUnit(line.quantitySold, line.unit))}</td>
        <td class="num mono">${money(line.revenue)}</td>
        <td class="num mono"><strong>${money(line.netProfit)}</strong></td>
      </tr>`).join("")
    : emptyRow(4, "За последние 30 дней продаж не было");

  setText("dash-soon-count", `${soon.length} ${plural(soon.length, ["позиция", "позиции", "позиций"])}`);
  $("dash-soon-body").innerHTML = soon.length
    ? soon.slice(0, 5).map((item) => `
      <tr>
        <td>
          <div class="cell-title">${esc(item.productName)}</div>
          <div class="cell-sub">${esc(item.supplierName)}</div>
        </td>
        <td class="num">${esc(qtyWithUnit(item.quantity, item.unit))}</td>
        <td class="num">
          <span class="pill ${item.daysLeft <= 2 ? "red" : "amber"}">${esc(daysLabel(item.daysLeft))}</span>
        </td>
      </tr>`).join("")
    : emptyRow(3, "Товаров с истекающим сроком нет");

  $("dash-ops-body").innerHTML = operations.length
    ? operations.map((item) => {
      const first = item.items[0];
      const extra = item.items.length > 1 ? ` + ещё ${item.items.length - 1}` : "";
      return `
        <tr>
          <td class="nowrap"><strong>${esc(item.number)}</strong></td>
          <td class="nowrap">${formatDate(item.occurredOn)}</td>
          <td><span class="reason ${reasonClasses[item.reason]}">${esc(reasonLabels[item.reason])}</span></td>
          <td>
            <div class="cell-title">${esc(first ? first.productName : "—")}${esc(extra)}</div>
            <div class="cell-sub">${item.items.length} ${plural(item.items.length, ["строка", "строки", "строк"])}</div>
          </td>
          <td class="num mono">${money(item.totalCost)}</td>
          <td class="num mono">${item.reason === "Sale" ? money(item.totalRevenue) : "—"}</td>
        </tr>`;
    }).join("")
    : emptyRow(6, "Операций пока нет");
}

/* ---------- Остатки ---------- */

async function loadStock() {
  try {
    state.stock = await api("/api/reports/stock");
    renderStock();
  } catch (error) {
    toast(error.message, true);
  }
}

function renderStock() {
  const query = $("stock-search").value.trim().toLowerCase();
  const items = state.stock.filter((item) =>
    !query ||
    item.name.toLowerCase().includes(query) ||
    item.sku.toLowerCase().includes(query) ||
    (item.category || "").toLowerCase().includes(query));

  $("stock-body").innerHTML = items.length
    ? items.map((item) => {
      const expiryClass = item.nearestExpiry ? "amber" : "gray";
      return `
        <tr>
          <td class="nowrap"><span class="pill gray">${esc(item.sku)}</span></td>
          <td><div class="cell-title">${esc(item.name)}</div></td>
          <td>${esc(item.category || "—")}</td>
          <td class="num"><strong>${esc(qtyWithUnit(item.quantity, item.unit))}</strong></td>
          <td class="num mono">${money(item.stockValue)}</td>
          <td><span class="pill ${expiryClass}">${item.nearestExpiry ? formatDate(item.nearestExpiry) : "без срока"}</span></td>
          <td class="num">${item.batches}</td>
        </tr>`;
    }).join("")
    : emptyRow(7, "Товар не найден");

  const totalUnits = state.stock.reduce((sum, item) => sum + item.quantity, 0);
  const totalValue = state.stock.reduce((sum, item) => sum + item.stockValue, 0);
  $("stock-total-units").textContent = `Всего на складе: ${qty(totalUnits)} ед. в ${state.stock.length} ${plural(state.stock.length, ["позиции", "позициях", "позициях"])}`;
  $("stock-total-value").textContent = `Стоимость запасов: ${money(totalValue)}`;
}

/* ---------- Поступления ---------- */

async function loadReceipts() {
  try {
    state.receipts = await api("/api/receipts?limit=40");
    renderReceipts();
  } catch (error) {
    toast(error.message, true);
  }
}

function renderReceipts() {
  setText("receipt-count", `${state.receipts.length} докум.`);

  $("receipt-body").innerHTML = state.receipts.length
    ? state.receipts.map((item) => {
      const first = item.items[0];
      const extra = item.items.length > 1 ? ` + ещё ${item.items.length - 1}` : "";
      return `
        <tr>
          <td class="nowrap"><strong>${esc(item.number)}</strong>${item.invoiceNumber ? `<div class="cell-sub">${esc(item.invoiceNumber)}</div>` : ""}</td>
          <td class="nowrap">${formatDate(item.receivedOn)}</td>
          <td>${esc(item.supplierName)}</td>
          <td>
            <div class="cell-title">${esc(first ? first.productName : "—")}${esc(extra)}</div>
            <div class="cell-sub">${esc(first ? `${qty(first.quantity)} ${first.unit} × ${money(first.unitCost)}` : "")}</div>
          </td>
          <td class="num mono"><strong>${money(item.totalCost)}</strong></td>
        </tr>`;
    }).join("")
    : emptyRow(5, "Поступлений пока нет");
}

/* ---------- Списания ---------- */

async function loadWriteOffs() {
  try {
    state.writeOffs = await api("/api/write-offs?limit=40");
    renderWriteOffs();
  } catch (error) {
    toast(error.message, true);
  }
}

function renderWriteOffs() {
  setText("wo-count", `${state.writeOffs.length} докум.`);

  $("wo-body").innerHTML = state.writeOffs.length
    ? state.writeOffs.map((item) => {
      const first = item.items[0];
      const extra = item.items.length > 1 ? ` + ещё ${item.items.length - 1}` : "";
      return `
        <tr>
          <td class="nowrap"><strong>${esc(item.number)}</strong></td>
          <td class="nowrap">${formatDate(item.occurredOn)}</td>
          <td><span class="reason ${reasonClasses[item.reason]}">${esc(reasonLabels[item.reason])}</span></td>
          <td>
            <div class="cell-title">${esc(first ? first.productName : "—")}${esc(extra)}</div>
            <div class="cell-sub">${esc(first ? `${qty(first.quantity)} ${first.unit} · ${item.comment || "без комментария"}` : "")}</div>
          </td>
          <td class="num mono">${money(item.totalCost)}</td>
          <td class="num mono">${item.reason === "Sale" ? money(item.totalRevenue) : "—"}</td>
        </tr>`;
    }).join("")
    : emptyRow(6, "Списаний пока нет");
}

/* ---------- Отчёты ---------- */

async function loadReports() {
  const from = $("report-from").value || isoDate(addDays(new Date(), -29));
  const to = $("report-to").value || isoDate(new Date());
  const taxPercent = Number($("report-tax").value || 6);
  const soonDays = Number($("soon-days").value || 7);

  if (from > to) {
    toast("Начало периода не может быть позже окончания", true);
    return;
  }

  if (!Number.isFinite(taxPercent) || taxPercent < 0 || taxPercent > 100) {
    toast("Ставка налога должна быть от 0 до 100%", true);
    return;
  }

  try {
    const [profit, expired, soon] = await Promise.all([
      api(`/api/reports/profit?from=${from}&to=${to}&taxRate=${taxPercent / 100}`),
      api(`/api/reports/expired-write-offs?from=${from}&to=${to}`),
      api(`/api/reports/soon-to-expire?days=${soonDays}`)
    ]);

    state.profit = profit;
    state.expired = expired;
    state.soon = soon;
    state.reportPeriod = { from, to, taxPercent, soonDays };

    renderProfitReport(profit);
    renderExpiredReport(expired);
    renderSoonReport(soon);
  } catch (error) {
    toast(error.message, true);
  }
}

function renderProfitReport(report) {
  const totals = report.totals;

  setText("rep-revenue", money(totals.revenue));
  setText("rep-cost", money(totals.cost));
  setText("rep-gross", money(totals.grossProfit));
  setText("rep-tax", money(totals.tax));
  setText("rep-net", money(totals.netProfit));
  setText("rep-qty", `${qty(totals.quantitySold)} ед.`);
  setText("rep-period-label", `${formatDate(report.from)} — ${formatDate(report.to)} · налог ${(report.taxRate * 100).toFixed(1).replace(".0", "")}%`);

  const maxNet = Math.max(...report.lines.map((line) => Math.abs(line.netProfit)), 1);

  $("rep-profit-body").innerHTML = report.lines.length
    ? report.lines.map((line) => {
      const share = Math.max(0, Math.round((line.netProfit / maxNet) * 100));
      return `
        <tr>
          <td>
            <div class="cell-title">${esc(line.name)}</div>
            <div class="cell-sub">${esc(line.sku)}</div>
          </td>
          <td class="num">${esc(qtyWithUnit(line.quantitySold, line.unit))}</td>
          <td class="num mono">${money(line.revenue)}</td>
          <td class="num mono">${money(line.cost)}</td>
          <td class="num mono">${money(line.grossProfit)}</td>
          <td class="num mono">${money(line.tax)}</td>
          <td class="num mono"><strong>${money(line.netProfit)}</strong></td>
          <td>
            <div class="share">
              <div class="share-bar"><i style="width:${share}%"></i></div>
              <span>${share}%</span>
            </div>
          </td>
        </tr>`;
    }).join("")
    : emptyRow(8, "За выбранный период продаж не было");

  $("rep-profit-foot").innerHTML = report.lines.length
    ? `<tr>
        <td>Итого</td>
        <td class="num">${esc(qty(totals.quantitySold))}</td>
        <td class="num">${money(totals.revenue)}</td>
        <td class="num">${money(totals.cost)}</td>
        <td class="num">${money(totals.grossProfit)}</td>
        <td class="num">${money(totals.tax)}</td>
        <td class="num">${money(totals.netProfit)}</td>
        <td></td>
      </tr>`
    : "";
}

function renderExpiredReport(items) {
  const totalLoss = items.reduce((sum, item) => sum + item.lossAmount, 0);
  setText("expired-total", `Потери: ${money(totalLoss)}`);

  $("expired-body").innerHTML = items.length
    ? items.map((item) => `
      <tr>
        <td class="nowrap">${formatDate(item.occurredOn)}<div class="cell-sub">${esc(item.number)}</div></td>
        <td>
          <div class="cell-title">${esc(item.productName)}</div>
          <div class="cell-sub">${esc(item.supplierName)}</div>
        </td>
        <td class="num">${esc(qtyWithUnit(item.quantity, item.unit))}</td>
        <td class="nowrap">${item.expiryDate ? formatDate(item.expiryDate) : "—"}</td>
        <td class="num mono">${money(item.lossAmount)}</td>
      </tr>`).join("")
    : emptyRow(5, "Списаний по сроку годности за период нет");
}

function renderSoonReport(items) {
  $("soon-body").innerHTML = items.length
    ? items.map((item) => {
      const pillClass = item.daysLeft <= 2 ? "red" : item.daysLeft <= 7 ? "amber" : "green";
      return `
        <tr>
          <td>
            <div class="cell-title">${esc(item.productName)}</div>
            <div class="cell-sub">${esc(item.supplierName)}</div>
          </td>
          <td class="num">${esc(qtyWithUnit(item.quantity, item.unit))}</td>
          <td class="nowrap">${formatDate(item.expiryDate)}</td>
          <td class="num"><span class="pill ${pillClass}">${esc(daysLabel(item.daysLeft))}</span></td>
          <td class="num mono">${money(item.quantity * item.unitCost)}</td>
        </tr>`;
    }).join("")
    : emptyRow(5, "Нет товаров с истекающим сроком");
}

/* ---------- Формы ---------- */

function syncReasonField() {
  const isSale = $("wo-reason").value === "Sale";
  $("wo-price").disabled = !isSale;
  $("wo-price").required = isSale;
  $("wo-price-field").style.opacity = isSale ? "1" : "0.55";
  $("wo-hint").textContent = isSale
    ? "При продаже списание идёт по FEFO: первыми уходят партии с ближайшим сроком годности."
    : "Для истечения срока списываются только просроченные партии; для порчи — любые партии по FEFO.";
}

async function refreshAfterMutation() {
  await loadReference();
  await Promise.all([loadDashboard(), loadStock(), loadReceipts(), loadWriteOffs()]);
  if (document.querySelector("#view-reports.active")) {
    await loadReports();
  }
}

function bindForms() {
  $("receipt-form").addEventListener("submit", async (event) => {
    event.preventDefault();
    const button = event.target.querySelector('button[type="submit"]');
    const label = button.textContent;
    button.disabled = true;
    button.textContent = "Сохраняем...";

    try {
      const payload = {
        supplierId: Number($("receipt-supplier").value),
        receivedOn: $("receipt-date").value,
        invoiceNumber: $("receipt-invoice").value.trim() || null,
        items: [{
          productId: Number($("receipt-product").value),
          quantity: Number($("receipt-qty").value),
          unitCost: Number($("receipt-cost").value),
          expiryDate: $("receipt-expiry").value || null
        }]
      };

      const created = await api("/api/receipts", {
        method: "POST",
        body: JSON.stringify(payload)
      });

      toast(`Поступление ${created.number} оформлено на ${money(created.totalCost)}`);
      $("receipt-qty").value = "24";
      $("receipt-invoice").value = "";
      await refreshAfterMutation();
      showView("receipts");
    } catch (error) {
      toast(error.message, true);
    } finally {
      button.disabled = false;
      button.textContent = label;
    }
  });

  $("writeoff-form").addEventListener("submit", async (event) => {
    event.preventDefault();
    const button = $("wo-submit");
    const label = button.textContent;
    button.disabled = true;
    button.textContent = "Списываем...";

    try {
      const reason = $("wo-reason").value;
      const payload = {
        occurredOn: $("wo-date").value,
        reason,
        comment: $("wo-comment").value.trim() || null,
        items: [{
          productId: Number($("wo-product").value),
          quantity: Number($("wo-qty").value),
          unitSalePrice: reason === "Sale" ? Number($("wo-price").value) : null
        }]
      };

      const created = await api("/api/write-offs", {
        method: "POST",
        body: JSON.stringify(payload)
      });

      const labelReason = reasonLabels[created.reason];
      toast(`${labelReason}: ${created.number} на сумму ${money(created.totalCost)}`);
      $("wo-comment").value = "";
      $("wo-qty").value = "5";
      await refreshAfterMutation();
      showView("writeoffs");
    } catch (error) {
      toast(error.message, true);
    } finally {
      button.disabled = false;
      button.textContent = label;
    }
  });

  $("wo-reason").addEventListener("change", syncReasonField);
  $("receipt-form").addEventListener("reset", () => setTimeout(resetDateDefaults, 0));

  $("stock-search").addEventListener("input", renderStock);
  $("stock-refresh").addEventListener("click", loadStock);

  $("report-run").addEventListener("click", loadReports);
  $("soon-refresh").addEventListener("click", loadReports);
}

function resetDateDefaults() {
  const today = new Date();
  $("receipt-date").value = isoDate(today);
  $("receipt-expiry").value = isoDate(addDays(today, 7));
  $("wo-date").value = isoDate(today);
}

function initDates() {
  resetDateDefaults();
  $("report-from").value = isoDate(addDays(new Date(), -29));
  $("report-to").value = isoDate(new Date());
}

/* ---------- Инициализация ---------- */

async function init() {
  initDates();
  bindForms();
  syncReasonField();

  document.querySelectorAll(".nav-item").forEach((button) => {
    button.addEventListener("click", () => showView(button.dataset.view));
  });

  document.querySelectorAll("[data-go]").forEach((button) => {
    button.addEventListener("click", () => showView(button.dataset.go));
  });

  $("todayLabel").textContent = new Date().toLocaleDateString("ru-RU", {
    weekday: "long",
    day: "numeric",
    month: "long",
    year: "numeric"
  });

  try {
    await loadReference();
    await Promise.all([
      loadDashboard(),
      loadStock(),
      loadReceipts(),
      loadWriteOffs(),
      loadReports()
    ]);
    toast("Данные склада загружены");
  } catch (error) {
    toast(`Не удалось загрузить данные: ${error.message}`, true);
  }
}

init();
