import "./styles.css";

const app = document.querySelector<HTMLDivElement>("#app");

if (!app) {
  throw new Error("App root not found.");
}

app.innerHTML = `
  <main class="app-shell">
    <section class="stage">
      <div class="video-shell">
        <p class="placeholder">Gesture debugger is being initialized.</p>
      </div>
    </section>
  </main>
`;
