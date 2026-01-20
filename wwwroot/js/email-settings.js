// Email Settings JavaScript
(function() {
    'use strict';

    // Initialize when DOM is ready
    document.addEventListener('DOMContentLoaded', function() {
        initializePasswordToggle();
        initializeFormValidation();
    })

    // ===== Password Toggle =====
    function initializePasswordToggle() {
        window.togglePassword = function (event) {
            event.preventDefault();
            const input = document.getElementById('FromPassword');
            const button = event.target.closest('.toggle-password');
            const eyeIcon = button.querySelector('.eye-icon');
            const eyeOffIcon = button.querySelector('.eye-off-icon');

            if (input && button) {
                if (input.type === 'text') {
                    // Chuyển sang ẩn mật khẩu
                    input.type = 'password';
                    if (eyeIcon) eyeIcon.style.display = 'block';
                    if (eyeOffIcon) eyeOffIcon.style.display = 'none';
                } else {
                    // Chuyển sang hiển thị mật khẩu
                    input.type = 'text';
                    if (eyeIcon) eyeIcon.style.display = 'none';
                    if (eyeOffIcon) eyeOffIcon.style.display = 'block';
                }
            }
        };
        
        // Khởi tạo trạng thái ban đầu - mặc định hiển thị mật khẩu (type="text")
        const input = document.getElementById('FromPassword');
        const button = document.getElementById('togglePasswordBtn');
        if (input && button) {
            // Nếu input có giá trị và đang là type="text", hiển thị icon ẩn
            if (input.value && input.type === 'text') {
                const eyeIcon = button.querySelector('.eye-icon');
                const eyeOffIcon = button.querySelector('.eye-off-icon');
                if (eyeIcon) eyeIcon.style.display = 'none';
                if (eyeOffIcon) eyeOffIcon.style.display = 'block';
            }
        }
    }

    // ===== Form Validation & Submission =====
    function initializeFormValidation() {
        const form = document.getElementById('settingsForm');

        if (form) {
            form.addEventListener('submit', function(e) {
                e.preventDefault();
                
                const emailInput = form.querySelector('input[name="FromEmail"]');
                const portInput = form.querySelector('input[name="SmtpPort"]');

                let isValid = true;
                let errorMessage = '';

                // Validate email
                if (emailInput && emailInput.value) {
                    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
                    if (!emailRegex.test(emailInput.value)) {
                        isValid = false;
                        errorMessage = 'Email không hợp lệ';
                        highlightError(emailInput);
                    }
                }

                // Validate port
                if (portInput && portInput.value) {
                    const port = parseInt(portInput.value);
                    if (isNaN(port) || port < 1 || port > 65535) {
                        isValid = false;
                        errorMessage = 'Port phải từ 1 đến 65535';
                        highlightError(portInput);
                    }
                }

                if (!isValid) {
                    showNotification(errorMessage, 'error');
                    return;
                }

                // Submit form via AJAX
                submitForm(form);
            });
        }
    }

    // ===== AJAX Form Submission =====
    function submitForm(form) {
        const formData = new FormData(form);
        const submitButton = form.querySelector('button[type="submit"]');
        const originalText = submitButton ? submitButton.textContent : '';
        
        // Disable submit button
        if (submitButton) {
            submitButton.disabled = true;
            submitButton.textContent = 'Đang lưu...';
        }

        // Get anti-forgery token
        const tokenInput = form.querySelector('input[name="__RequestVerificationToken"]');
        if (tokenInput) {
            formData.append('__RequestVerificationToken', tokenInput.value);
        }

        fetch(form.action, {
            method: 'POST',
            body: formData
        })
        .then(function(response) {
            if (response.ok) {
                return response.text();
            }
            throw new Error('Network response was not ok');
        })
        .then(function(html) {
            // Show success notification
            showNotification('Cập nhật cấu hình email thành công!', 'success');
            
            // Reload page after 2 seconds to show updated data
            setTimeout(function() {
                window.location.reload();
            }, 2000);
        })
        .catch(function(error) {
            console.error('Error:', error);
            showNotification('Có lỗi xảy ra khi lưu cấu hình. Vui lòng thử lại.', 'error');
            
            // Re-enable submit button
            if (submitButton) {
                submitButton.disabled = false;
                submitButton.textContent = originalText;
            }
        });
    }

    // ===== Helper Functions =====
    function highlightError(input) {
        input.classList.add('error');
        input.focus();

        setTimeout(function() {
            input.classList.remove('error');
        }, 3000);
    }

    function showNotification(message, type) {
        type = type || 'info';
        
        // Remove existing notifications
        const existing = document.querySelector('.notification');
        if (existing) {
            existing.remove();
        }

        // Create notification element
        const notification = document.createElement('div');
        notification.className = 'notification notification-' + type;
        
        const closeSvg = '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="18" y1="6" x2="6" y2="18"></line><line x1="6" y1="6" x2="18" y2="18"></line></svg>';
        const iconSvg = getNotificationIcon(type);
        
        notification.innerHTML = 
            '<div class="notification-content">' +
                '<span class="notification-icon">' + iconSvg + '</span>' +
                '<span class="notification-message">' + message + '</span>' +
                '<button class="notification-close" onclick="this.parentElement.parentElement.remove()">' +
                    closeSvg +
                '</button>' +
            '</div>';

        // Add to page
        document.body.appendChild(notification);

        // Auto-remove after 4 seconds
        setTimeout(function() {
            notification.classList.add('notification-fade-out');
            setTimeout(function() {
                notification.remove();
            }, 300);
        }, 4000);
    }

    function getNotificationIcon(type) {
        const icons = {
            success: '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path><polyline points="22 4 12 14.01 9 11.01"></polyline></svg>',
            error: '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"></circle><line x1="15" y1="9" x2="9" y2="15"></line><line x1="9" y1="9" x2="15" y2="15"></line></svg>',
            warning: '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"></path><line x1="12" y1="9" x2="12" y2="13"></line><line x1="12" y1="17" x2="12.01" y2="17"></line></svg>',
            info: '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="16" x2="12" y2="12"></line><line x1="12" y1="8" x2="12.01" y2="8"></line></svg>'
        };
        return icons[type] || icons.info;
    }
})();
