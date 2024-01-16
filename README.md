# CanoeService

**WORK IN PROGRESS!**

Canoe is a websocket server for controlling Amateur Radio hardware on a Windows Machine. 

* Sends audio (PCM) from an audio output device (headphones) to a websocket
* Receives audio (PCM) from websocket and sends it to an audio input device (microphone)
* Send and Recieve commands and status

TODO:

* Add low-latecy Opus codec encode/decode
* Refine command processor
* Start/stop audio streams
* Choose audio codec (PCM|Opus)
* Choose number of channels (radio only needs 1 channel)
* Choose audio device sample rate
* Calculate end-to-end latency
* Add rotator control
* Add front-end