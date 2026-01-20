(function($) {
    $(function() {
        var frame;
        $('#obw_logo_upload_button').on('click', function(e) {
            e.preventDefault();

            if (frame) {
                frame.open();
                return;
            }

            frame = wp.media({
                title: 'Select Station Logo',
                button: { text: 'Use this logo' },
                multiple: false
            });

            frame.on('select', function() {
                var attachment = frame.state().get('selection').first().toJSON();
                $('#obw_logo_url').val(attachment.url);
            });

            frame.open();
        });
    });
})(jQuery);
