// Scroll to top on page load
window.onload = function () {
  window.scrollTo(0, 0);
};

// Add animation classes on scroll
document.addEventListener("DOMContentLoaded", function () {
  const animateElements = document.querySelectorAll(
    ".card, .navbar, .hero-section .card"
  );

  const observer = new IntersectionObserver(
    (entries) => {
      entries.forEach((entry) => {
        if (entry.isIntersecting) {
          entry.target.classList.add("animate-fade-in");
        }
      });
    },
    { threshold: 0.1 }
  );

  animateElements.forEach((element) => {
    observer.observe(element);
  });

  // Form validation
  const form = document.querySelector("form");
  if (form) {
    form.addEventListener("submit", function (e) {
      e.preventDefault();

      const pickupDate = document.getElementById("pickupDate").value;
      const dropoffDate = document.getElementById("dropoffDate").value;
      const carType = document.getElementById("carType").value;

      if (!pickupDate || !dropoffDate || carType === "Select a category") {
        alert("Please fill in all fields");
        return;
      }

      if (new Date(pickupDate) >= new Date(dropoffDate)) {
        alert("Drop-off date must be after pick-up date");
        return;
      }

      alert("Search successful! Redirecting to results...");
    });
  }

  // Smooth scroll for navigation links
  document.querySelectorAll('a[href^="#"]').forEach((anchor) => {
    anchor.addEventListener("click", function (e) {
      e.preventDefault();
      const target = document.querySelector(this.getAttribute("href"));
      if (target) {
        target.scrollIntoView({
          behavior: "smooth",
          block: "start",
        });
      }
    });
  });
});
