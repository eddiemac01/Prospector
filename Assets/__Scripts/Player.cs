using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq; // Enables LINQ queries, which will be explained soon

// The player can either be human or an ai
public enum PlayerType {
	human,
	ai	
}
[System.Serializable] // Make the Player class visible in the Inspector pane
public class Player {
	public PlayerType         type = PlayerType.ai;
	public int                playerNum;
	public List<CardBartok>   hand; // The cards in this player's hand
	public SlotDef            handSlotDef;
	
	// Add a card to the hand
	public CardBartok AddCard(CardBartok eCB) {
		if (hand == null) hand = new List<CardBartok>();
		// Add the card to the hand
		hand.Add (eCB);
		// Sort the cards by rank using LINQ if this is a human
		if (type == PlayerType.human) {
			CardBartok[] cards = hand.ToArray(); // Copy hand to a new array
			
			cards = cards.OrderBy( cd => cd.rank ).ToArray();
			
			// Convert the array CardBartok[] back to a List<CardBartok>
			
			hand = new List<CardBartok>(cards);

		}
		eCB.SetSortingLayerName("10"); // This sorts the moving card to the top
		eCB.eventualSortLayer = handSlotDef.layerName;
		FanHand ();
		return( eCB );	
	}
	// Remove a card from the hand
	public CardBartok RemoveCard(CardBartok cb) {
		hand.Remove(cb);
		FanHand ();
		return(cb);	
	}
	
	public void FanHand() {
		// startRot is the rotation about Z of the first card
		float startRot = 0;
		startRot = handSlotDef.rot;
		if (hand.Count > 1) {
			startRot += Bartok.S.handFanDegrees * (hand.Count-1) / 2;	
		}
		
		Vector3 pos;
		
		float rot;
		
		Quaternion rotQ;
		
		for (int i=0; i<hand.Count; i++) {
			
			rot = startRot - Bartok.S.handFanDegrees*i; // Rot about the z axis

			rotQ = Quaternion.Euler( 0, 0, rot );

			
			pos = Vector3.up * CardBartok.CARD_HEIGHT / 2f;

			
			pos = rotQ * pos;

			pos += handSlotDef.pos;

			pos.z = -0.5f*i;

			if (Bartok.S.phase != TurnPhase.idle) {
				hand[i].timeStart = 0;	
			}
			
			hand[i].MoveTo(pos, rotQ); // Tell CardBartok to interpolate
			
			hand[i].state = CBState.toHand;
			
			hand[i].faceUp = (type == PlayerType.human);
			
			// Set the SortOrder of the cards so that they overlap properly
			hand[i].eventualSortOrder = i*4;
			//hand[i].SetSortOrder(i*4);	
		}
	}
	
	// The TakeTurn() function enables the AI of the computer Players
	public void TakeTurn() {
		Utils.tr (Utils.RoundToPlaces(Time.time), "Player.TakeTurn");
		// Don't need to do anything if this is the human player.
		if (type == PlayerType.human) return;
		Bartok.S.phase = TurnPhase.waiting;
		CardBartok cb;
		// If this is an AI player, need to make a choice about what to play
		// Find valid plays
		List<CardBartok> validCards = new List<CardBartok>();
		foreach (CardBartok tCB in hand) {
			if (Bartok.S.ValidPlay(tCB)) {
				validCards.Add ( tCB );	
			}
		}
		
		// If there are no valid cards
		if (validCards.Count == 0) {
			// ...then draw a card
			cb = AddCard( Bartok.S.Draw () );
			cb.callbackPlayer = this;
			return;	
		}
		// Otherwise, if there is a card or more to play, pick one
		cb = validCards[ Random.Range (0,validCards.Count) ];
		RemoveCard(cb);
		Bartok.S.MoveToTarget(cb);
		cb.callbackPlayer  = this;
	}
	
	public void CBCallback(CardBartok tCB) {
		Utils.tr (Utils.RoundToPlaces(Time.time), "Player.CBCallback()",tCB.name,"Player "+playerNum);
		// The card is done moving, so pass the turn
		Bartok.S.PassTurn();		
	}
	
}