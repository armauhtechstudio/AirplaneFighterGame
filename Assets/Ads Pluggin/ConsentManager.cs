using System;
using System.Collections.Generic;
using System.Diagnostics;
using GoogleMobileAds.Ump.Api;
using UnityEngine;
public class ConsentManager : MonoBehaviour
{
    ConsentForm _consentForm;
    void Start()
    {

        ConsentRequestParameters request = new ConsentRequestParameters
        {
            TagForUnderAgeOfConsent = false,
        };

        ConsentInformation.Update(request, OnConsentInfoUpdated);
    }

    void OnConsentInfoUpdated(FormError error)
    {
        if (error != null)
        {
            UnityEngine.Debug.LogError(error);
            return;
        }

        if (ConsentInformation.IsConsentFormAvailable())
        {
            LoadConsentForm();
        }
    }

    void LoadConsentForm()
    {
        ConsentForm.Load(OnLoadConsentForm);
    }

    void OnLoadConsentForm(ConsentForm consentForm, FormError error)
    {
        if (error != null)
        {
            UnityEngine.Debug.LogError(error);
            return;
        }

        _consentForm = consentForm;

        if (ConsentInformation.ConsentStatus == ConsentStatus.Required)
        {
            _consentForm.Show(OnShowForm);
        }
    }


    void OnShowForm(FormError error)
    {
        if (error != null)
        {
            UnityEngine.Debug.LogError(error);
            return;
        }

        LoadConsentForm();
    }

}