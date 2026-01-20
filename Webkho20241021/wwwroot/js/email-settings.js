// Toggle password visibility
function togglePassword(event) {
    event.preventDefault();
    const passwordInput = document.getElementById('FromPassword');
    const toggleBtn = document.getElementById('togglePasswordBtn');
    const eyeOffIcon = toggleBtn.querySelector('.eye-off-icon');
    const eyeIcon = toggleBtn.querySelector('.eye-icon');

    if (passwordInput.type === 'password') {
        passwordInput.type = 'text';
        eyeOffIcon.style.display = 'none';
        eyeIcon.style.display = 'block';
    } else {
        passwordInput.type = 'password';
        eyeOffIcon.style.display = 'block';
        eyeIcon.style.display = 'none';
    }
}

// Initialize on page load
document.addEventListener('DOMContentLoaded', function() {
    // Ensure password field starts as password type
    const passwordInput = document.getElementById('FromPassword');
    if (passwordInput && passwordInput.type !== 'password') {
        passwordInput.type = 'password';
    }
});
