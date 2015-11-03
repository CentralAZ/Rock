﻿<%@ Control Language="C#" AutoEventWireup="true" CodeFile="TakeProfilePhoto.ascx.cs" Inherits="RockWeb.Plugins.com_centralaz.Widgets.TakeProfilePhoto" %>
<meta name="viewport" content="width=device-width, initial-scale=0.75, user-scalable=no" />
<asp:UpdatePanel ID="upnlContent" runat="server">
    <ContentTemplate>
        <asp:Panel ID="pnlDetails" runat="server" CssClass="row">
            <center>
            <h3>
                <div id="divLoginWarning" runat="server" visible="false">
                    You're not logged in, so you won't be able to save photos.
                </div>
            </h3>
            <h3>Take photo for: 
                <asp:TextBox ID="txtName" placeholder="first & last name" runat="server" type="text" autocomplete='off' spellcheck='false' autocorrect='off' onkeydown="delayExecute(this);" AutoPostBack="false" />
            </h3>

            <input type="hidden" id="selectedPersonId" />
            <div id="personSearching" class="progress" style="display: none">
                <div class="progress-bar progress-bar-striped active" style="width: 100%;"></div>
            </div>
            <div id="divPeople"></div>

            <div id="video_box">
                    <video id="video" width="425" height="425" autoplay>Your browser does not support this streaming content.</video>
            </div>
            <canvas id="canvas" width="425" height="425" style="display:none"></canvas>


            <div id="uploadProgress" class="progress" style="display: none">
                <div class="progress-bar progress-bar-striped active" style="width: 100%;"></div>
            </div>
            <div id="photoUploadMessage" class="alert alert-success" style="display: none; width: 80%;"></div>

                <div class="well">
            <div class="row">
                <div class="col-md-6 col-sm-6 col-xs-6">
                    <asp:Button runat="server" ID="btnStart" Text="Start" class="btn btn-primary btn-lg" OnClientClick="return false;" UseSubmitBehavior="false" CausesValidation="false" />
                    <asp:Button runat="server" ID="btnStop" Text="Cancel" class="btn btn-default btn-lg" Style="display: none;" OnClientClick="return false;" UseSubmitBehavior="false" CausesValidation="false" />
                    <a href="#" id="btnSwap" class="btn btn-default" style="display: none;">
                        <img src='<%= ResolveRockUrl( "~/Plugins/com_centralaz/Widgets/Assets/Icons/camera-swap.png") %>' />
                        Swap</a>
                </div>
                <div class="col-md-6 col-sm-6 col-xs-6">
                    <asp:Button runat="server" ID="btnPhoto" Text="Take photo" class="btn btn-success btn-lg" Style="display: none;" OnClientClick="return false;" UseSubmitBehavior="false" CausesValidation="false" />
                    <asp:Button runat="server" ID="btnRedo" Text="Re-do" class="btn btn-default btn-lg" Style="display: none;" OnClientClick="return false;" UseSubmitBehavior="false" CausesValidation="false" />
                    <asp:Button runat="server" ID="btnUpload" Text="Upload" class="btn btn-warning btn-lg" Style="display: none;" OnClientClick="return false;" UseSubmitBehavior="false" CausesValidation="false" />
                </div>
            </div>
                </div>
            </center>
        </asp:Panel>
        <script type="text/javascript">

            var typingTimer;
            var doneTypingInterval = 600;

            ///
            /// Detect keystroke and only execute after the user has finish typing
            ///
            function delayExecute(e) {
                clearTimeout(typingTimer);
                typingTimer = setTimeout(
                    function () { search(e) },
                    doneTypingInterval
                );

                if (event.keyCode == 13) {
                    event.returnValue = false;
                    event.cancel = true;
                }

                return false;
            }

            ///
            /// Perform firstName lastName search and display results in a set of LI.
            ///
            function search(e) {

                // must supply more than three characters
                if (e.value.replace(/\s/g, '').length <= 3) {
                    return;
                }

                $('#personSearching').fadeIn('fast');

                // clear previous list of people
                var divPeople = $('div[id$="divPeople"]');
                divPeople.html('');

                var name = e.value.trim();
                var idx = name.indexOf(" ");

                var firstName = "", lastname = "";
                // if only one word was given assume it's the lastname
                if (idx == -1) {
                    lastName = name;
                }
                else {
                    firstName = name.substring(0, idx).trim();
                    lastName = name.substring(idx + 1).trim();
                }

                var data = "{'firstName': '" + firstName + "', 'lastName': '" + lastName + "', 'count': " + 20 + " }";
                //console.log(data);
                var request = $.ajax({
                    type: "GET",
                    url: '<%=ResolveUrl("~/api/People/Search/") %>?name=' + encodeURIComponent(firstName + " " + lastName) + '&includeHtml=false&includeDetails=true&includeBusinesses=false&includeDeceased=true',
                    data: data,
                    contentType: "application/json",
                    dataType: "json",
                    success: function (result) {
                        console.log("success");
                        if (result.length > 0) {
                            divPeople.append("<ul>");
                            for (var i = 0; i < result.length && i <= 20; i++) {
                                var extraDetails = "";
                                if (result[i].Address != null) {
                                    extraDetails = result[i].Address;
                                }
                                if (result[i].SpouseName != null ) {
                                    extraDetails += ( extraDetails.length > 0 ? "; " : "" ) + "spouse: " + result[i].SpouseName;
                                }
                                if ( extraDetails.length > 0 ) {
                                    extraDetails = " (" + extraDetails + ")";
                                }

                                divPeople.append("<li class='btn btn-default btn-lg btn-block' onclick='clickPerson(this)' id='" + result[i].Id + "'>" + result[i].Name + extraDetails + "</li>");
                            }

                            divPeople.append("<ul>");
                            $('#personSearching').hide();

                            return true;
                        } else {
                            $('#personSearching').hide();
                            //alert('No one found.');
                        }
                    },
                    error: function (req, status, err) {
                        console.log("something went wrong: " + status + " error " + err);
                        return true;
                    }
                });
            }

            ///
            /// Handler function when they click a person in the search results list.
            ///
            function clickPerson(e) {
                unselectPerson();
                $('input[id="selectedPersonId"]').val(e.id);
                $(e).removeClass('btn-default');
                $(e).addClass('btn-primary');
            }

            ///
            /// Handler function to unselect any existing selected person.
            ///
            function unselectPerson(e) {
                $('input[id="selectedPersonId"]').val('');
                $("li").removeClass('btn-primary');
                $("li").addClass('btn-default');
            }

            ///
            /// Click event handler for selecting a person from the list.
            ///
            //$(function () {
            //    $("li.btn").on("click", function () {
            //        $("li").addClass('btn-default');
            //        $(this).removeClass('btn-default');
            //        $(this).addClass('btn-primary');
            //    });
            //});

            ///
            /// initialize items on this page and set up for video shoot and capture.
            ///
            function initialize() {
                $('input[id$="btnPhoto"]').attr('disabled', 'disabled');
                $('input[id$="btnUpload"]').attr('disabled', 'disabled');

                // override below via MediaStreamTrack.getSources() call...
                var constraints = {
                    video: {
                        mandatory: {
                            maxWidth: 425,
                            maxHeight: 425,
                            minWidth: 425,
                            minHeight: 425
                        }
                    }
                };

                var allowSwap = false;
                var defaultFacing = "environment";
                var videoSourceId = '';
                var altVideoSourceId = '';
                var usingVideoSourceId = '';

                // This next section is not quite working.
                MediaStreamTrack.getSources(gotSources);
                function gotSources(sourceInfos) {
                    for (var i = 0; i != sourceInfos.length; ++i) {
                        var sourceInfo = sourceInfos[i];
                        console.log(sourceInfo.kind + '->' + sourceInfo.facingMode + ' id: ' + sourceInfo.id);
                        if (sourceInfo.kind == 'video' && sourceInfo.facing == defaultFacing) {
                            videoSourceId = sourceInfo.id;
                            allowSwap = true;
                            usingVideoSourceId = videoSourceId;
                        }
                        else if (sourceInfo.kind == 'video' && sourceInfo.facing != defaultFacing) {
                            altVideoSourceId = sourceInfo.id;
                        }
                    }

                    constraints = {
                        video: {
                            mandatory: {
                                maxWidth: 425,
                                maxHeight: 425,
                                minWidth: 425,
                                minHeight: 425
                            },
                            optional: [{ sourceId: videoSourceId }]
                        }
                    };
                }

                var canvas = document.getElementById("canvas"),
                    context = canvas.getContext("2d"),
                    video = document.getElementById("video"),
                    //videoObj = { "video": true },
                    //videoObj = { video: { mandatory: { minAspectRatio: 1.0, maxAspectRatio: 1.00 } } },
                    btnStart = document.getElementById('<%=btnStart.ClientID %>'),
                    btnRedo = document.getElementById('<%=btnRedo.ClientID %>'),
                    btnStop = document.getElementById('<%=btnStop.ClientID %>'),
                    btnSwap = document.getElementById('btnSwap'),
                    btnPhoto = document.getElementById('<%=btnPhoto.ClientID %>'),
                    btnUpload = document.getElementById('<%=btnUpload.ClientID %>'),
                    errBack = function (error) {
                        console.log("Video capture error: ", error.code);
                        alert("error with camera: " + error.code);
                    };

                function getAndStartVideo(constraints) {
                    if (navigator.getUserMedia) { // standard
                        navigator.getUserMedia(constraints, function (stream) {
                            video.src = stream;
                            video.play();
                            localMediaStream = stream;
                        }, errBack)
                    } else if (navigator.webkitGetUserMedia) {
                        navigator.webkitGetUserMedia(constraints, function (stream) {
                            video.src = window.webkitURL.createObjectURL(stream);
                            video.play();
                            localMediaStream = stream;
                        }, errBack);
                    } else if (navigator.mozGetUserMedia) {
                        navigator.mozGetUserMedia(constraints, function (stream) {
                            video.src = window.URL.createObjectURL(stream);
                            video.play();
                            localMediaStream = stream;
                        }, errBack);
                    }
                }

                ///
                /// Hides the canvas and shows the video (useful for re-do or camera swap).
                ///
                function hideCanvasAndShowVideo() {
                    $('canvas[id$="canvas"]').hide();
                    $('#video_box').show();
                    $('input[id$="btnRedo"]').hide();
                    $('input[id$="btnPhoto"]').show().removeAttr('disabled');
                    $('input[id$="btnUpload"]').attr('disabled', 'disabled');
                    $('#photoUploadMessage').hide();
                }

                ///
                /// Handles the start of video play for snapshot capture.
                ///
                btnStart.addEventListener("click", function (e) {
                    $('input[id$="btnPhoto"]').removeAttr('disabled');
                    $('input[id$="btnStart"]').hide();
                    $('input[id$="btnStop"]').show();
                    $('input[id$="btnPhoto"]').show();
                    $('canvas[id$="canvas"]').hide();
                    $('#video_box').fadeIn('fast');

                    if (allowSwap) {
                        $('a[id$="btnSwap"]').show();
                    }

                    var localMediaStream;
                    getAndStartVideo(constraints);
                });

                ///
                /// Handles the swap camera button click event.
                ///
                btnSwap.addEventListener("click", function (e) {
                    $('canvas[id$="canvas"]').fadeOut("slow");
                    if (localMediaStream) {
                        localMediaStream.stop();
                    }

                    if (usingVideoSourceId == videoSourceId) {
                        usingVideoSourceId = altVideoSourceId;
                    } else {
                        usingVideoSourceId = videoSourceId;
                    }

                    constraints = {
                        video: {
                            mandatory: {
                                maxWidth: 425,
                                maxHeight: 425,
                                minWidth: 425,
                                minHeight: 425
                            },
                            optional: [{ sourceId: usingVideoSourceId }]
                        }
                    };

                    hideCanvasAndShowVideo();
                    getAndStartVideo(constraints);

                });

                ///
                /// Take Photo click handler.
                ///
                /// Draw image, hide video, show snapshot, enable upload button,
                /// and hide the take photo button and show the re-do button (just in case they blinked ;)
                ///
                btnPhoto.addEventListener("click", function () {
                    context.drawImage(video, 0, 0, 425, 425);
                    $('#video_box').hide();
                    $('canvas[id$="canvas"]').fadeIn();
                    $('input[id$="btnPhoto"]').hide();
                    $('input[id$="btnUpload"]').show().removeAttr('disabled');
                    $('input[id$="btnRedo"]').show();
                    $('#photoUploadMessage').hide();
                });

                ///
                /// Re-Do click handler.
                ///
                /// Hide the snapshot, show the video, hide the redo button,
                /// enable the take photo button, disable the upload button
                ///
                btnRedo.addEventListener("click", function () {
                    hideCanvasAndShowVideo();
                });

                ///
                /// Stop click handler.
                ///
                /// Stop the action, reset,.
                ///
                btnStop.addEventListener("click", function () {
                    stopVideo();
                });

                ///
                /// Stops the video stream and clears most everything.
                ///
                function stopVideo() {
                    if (localMediaStream) {
                        localMediaStream.stop();
                    }
                    clearPeople();
                    $('canvas[id$="canvas"]').fadeOut("slow");
                    $('#video_box').fadeOut("slow");
                    $('input[id$="btnStop"]').hide();
                    $('a[id$="btnSwap"]').hide();
                    $('input[id$="btnStart"]').show();
                    $('input[id$="btnPhoto"]').hide();
                    $('input[id$="btnRedo"]').hide();
                    $('input[id$="btnUpload"]').hide().attr('disabled', 'disabled');
                }

                ///
                /// Clear everyone from the list and unselect anything in the hidden field.
                ///
                function clearPeople() {
                    var divPeople = $('div[id$="divPeople"]');
                    divPeople.html('');
                    $('input[id="selectedPersonId"]').val('');
                    $('input[id="txtName"]').val('');
                }

                ///
                /// Upload Photo click handler.
                ///
                /// Upload the photo to the photo save webservice.
                ///
                btnUpload.addEventListener("click", function () {

                    // This png often errors out trying to parse base64 on the server.
                    //var dataUrl = canvas.toDataURL("png");
                    var dataUrl = canvas.toDataURL("image/jpeg", 0.95);

                    console.log(dataUrl);

                    var personId = $('input[id="selectedPersonId"]').val();
                    if (personId == "") {
                        alert("Sorry, you have to pick a person first.");
                        return false;
                    }

                    $('#uploadProgress').fadeIn('fast');
                    var data = {
                        img64: dataUrl
                    }
                    console.log(JSON.stringify(data));
                    // post the photo image to the server for the selected person.
                    var request = $.ajax({
                        type: "POST",
                        url: '<%=ResolveUrl("~/api/People/AddPhotoToPerson") %>?personId=' + personId,
                        data: JSON.stringify(dataUrl),
                        contentType: "application/json",
                        dataType: "json",
                        success: function (result) {
                            $('#uploadProgress').hide();

                            $('#photoUploadMessage').removeClass('alert-error').addClass('alert-success').html('<i class="icon-ok"></i> Success');
                            $('#photoUploadMessage').fadeIn('fast').delay(9000).fadeOut('slow');
                            $('input[id$="btnUpload"]').attr('disabled', 'disabled');
                            unselectPerson();
                            stopVideo();
                            clearPeople();
                            return true;
                        },
                        error: function (req, status, err) {
                            $('#uploadProgress').fadeOut('fast');
                            console.log("something went wrong: " + status + " error " + err);
                            $('#photoUploadMessage').removeClass('alert-success').addClass('alert-error').html(err).fadeIn('fast');
                            return true;
                        }
                    });
                });

            };

            // Why this and not $(function...), I was having some trouble and this way seemed to work consistantly.
            window.onload = function () {
                initialize();
            }
        </script>
    </ContentTemplate>

</asp:UpdatePanel>
