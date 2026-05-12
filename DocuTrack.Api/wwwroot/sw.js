self.addEventListener('push', function (event) {
    let title = 'DocuTrack';
    let body = 'You have a new notification.';

    if (event.data) {
        try {
            const data = event.data.json();
            title = data.title || title;
            body = data.message || body;
        } catch (e) {
            // plain text fallback
            body = event.data.text();
        }
    }

    const options = {
        body: body,
        icon: '/icon.png',
        badge: '/icon.png',
        vibrate: [100, 50, 100],
        data: { url: '/' }
    };

    event.waitUntil(self.registration.showNotification(title, options));
});

self.addEventListener('notificationclick', function (event) {
    event.notification.close();
    event.waitUntil(clients.openWindow(event.notification.data.url || '/'));
});