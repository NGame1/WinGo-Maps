﻿using System;
using System.Linq;
using Windows.Devices.Geolocation;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;
using static GoogleMapsUnofficial.ViewModel.DirectionsControls.DirectionsHelper;

namespace GoogleMapsUnofficial.ViewModel.VoiceNavigation
{
    public class VoiceHelper : IDisposable
    {
        static VoiceHelper AvailableInstance { get; set; }
        DateTime LastWarn { get; set; }
        static double DistanceTo(double lat1, double lon1, double lat2, double lon2, char unit = 'K')
        {
            double rlat1 = Math.PI * lat1 / 180;
            double rlat2 = Math.PI * lat2 / 180;
            double theta = lon1 - lon2;
            double rtheta = Math.PI * theta / 180;
            double dist =
                Math.Sin(rlat1) * Math.Sin(rlat2) + Math.Cos(rlat1) *
                Math.Cos(rlat2) * Math.Cos(rtheta);
            dist = Math.Acos(dist);
            dist = dist * 180 / Math.PI;
            dist = dist * 60 * 1.1515;
            switch (unit)
            {
                case 'K': //Kilometers -> default
                    return dist * 1.609344;
                case 'N': //Nautical Miles 
                    return dist * 0.8684;
                case 'M': //Miles
                    return dist;
            }
            return dist;
        }
        Route Route; Step LastStep = null;
        public VoiceHelper(Route route)
        {
            Route = route;
            MapViewVM.GeoLocate.PositionChanged += GeoLocate_PositionChanged;
            if(AvailableInstance != null)
            {
                AvailableInstance.Dispose();
            }
            AvailableInstance = this;
        }
        
        ~VoiceHelper()
        {
            MapViewVM.GeoLocate.PositionChanged -= GeoLocate_PositionChanged;
            Route = null;
            LastStep = null;
        }

        private async void GeoLocate_PositionChanged(Geolocator sender, PositionChangedEventArgs args)
        {
            var cp = args.Position.Coordinate;
            if (DateTime.Now.Subtract(LastWarn).TotalSeconds < 6) return;
            LastWarn = DateTime.Now;
            foreach (var items in Route.legs)
            {
                foreach (var item in items.steps)
                {
                    var d = DistanceTo(cp.Point.Position.Latitude, cp.Point.Position.Longitude, item.end_location.lat, item.end_location.lng);
                    if (d <= 0.4)
                    {
                        if (LastStep != item)
                        {
                            using (var speech = new SpeechSynthesizer())
                            {
                                var mediaplayer = new MediaPlayer() { AudioCategory = MediaPlayerAudioCategory.Other };
                                speech.Voice = SpeechSynthesizer.AllVoices.First(gender => gender.Gender == VoiceGender.Female);
                                if (item.maneuver == null)
                                {
                                    SpeechSynthesisStream stream = await speech.SynthesizeTextToStreamAsync(item.html_instructions.NoHTMLString());
                                    mediaplayer.Source = MediaSource.CreateFromStream(stream, stream.ContentType);
                                    mediaplayer.Play();
                                }
                                else
                                {
                                    SpeechSynthesisStream stream = await speech.SynthesizeTextToStreamAsync(item.maneuver + "\n" + item.html_instructions.NoHTMLString());
                                    mediaplayer.Source = MediaSource.CreateFromStream(stream, stream.ContentType);
                                    mediaplayer.Play();
                                }
                            }
                            LastStep = item;
                            var r = Route.legs.FirstOrDefault().steps.Where(x => DistanceTo(cp.Point.Position.Latitude, cp.Point.Position.Longitude, x.end_location.lat, x.end_location.lng) < 0.02);
                            if (r.Any())
                            {
                                var res = Route.legs.FirstOrDefault().steps.ToList();
                                res.Remove(r.FirstOrDefault());
                                Route.legs.FirstOrDefault().steps = res.ToArray();
                            }
                            return;
                        }
                    }
                }
            }
        }

        public void Dispose()
        {
            MapViewVM.GeoLocate.PositionChanged -= GeoLocate_PositionChanged;
            Route = null;
            LastStep = null;
        }
    }
}
