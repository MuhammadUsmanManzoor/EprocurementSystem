import type { Config } from "tailwindcss";

const config: Config = {
  content: ["./src/**/*.{ts,tsx}"],
  theme: {
    extend: {
      colors: {
        ink: "#172033",
        line: "#d9dee8",
        field: "#f6f7f9",
        brand: "#0f766e",
        accent: "#b45309"
      }
    }
  },
  plugins: []
};

export default config;
