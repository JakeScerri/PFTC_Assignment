// In your site.js file or inline script
$(document).ready(function() {
    let isSubmitting = false;

    $('#ticketForm').submit(function(e) {
        e.preventDefault();

        // Prevent duplicate submissions
        if (isSubmitting) {
            return false;
        }

        isSubmitting = true;
        $('#submitButton').prop('disabled', true); // Disable the button

        var formData = new FormData();
        formData.append('title', $('#title').val());
        formData.append('description', $('#description').val());
        formData.append('priority', $('#priority').val());

        // Add files
        var fileInput = $('#screenshots')[0];
        for (var i = 0; i < fileInput.files.length; i++) {
            formData.append('screenshots', fileInput.files[i]);
        }

        // Show progress bar
        $('.progress').removeClass('d-none');

        $.ajax({
            url: '/api/tickets',
            type: 'POST',
            data: formData,
            processData: false,
            contentType: false,
            xhr: function() {
                var xhr = new XMLHttpRequest();
                xhr.upload.addEventListener('progress', function(e) {
                    if (e.lengthComputable) {
                        var percent = Math.round((e.loaded / e.total) * 100);
                        $('#uploadProgress').css('width', percent + '%');
                        $('#uploadProgress').text(percent + '%');
                    }
                }, false);
                return xhr;
            },
            success: function(response) {
                // Show success message
                $('#submitResult').removeClass('d-none alert-danger').addClass('alert-success');
                $('#submitResult').html('Ticket submitted successfully! You can track its status later.');

                // Reset form
                $('#ticketForm')[0].reset();

                // Hide progress bar
                $('.progress').addClass('d-none');
                $('#uploadProgress').css('width', '0%');

                // Re-enable form submission after a delay
                setTimeout(function() {
                    isSubmitting = false;
                    $('#submitButton').prop('disabled', false);
                }, 3000);
            },
            error: function(error) {
                // Show error message
                $('#submitResult').removeClass('d-none alert-success').addClass('alert-danger');
                $('#submitResult').text('Error submitting ticket: ' + error.responseText);

                // Hide progress bar
                $('.progress').addClass('d-none');

                // Re-enable form submission
                isSubmitting = false;
                $('#submitButton').prop('disabled', false);
            }
        });
    });
});