using UnityEngine;
using System.Collections;
using System.Collections.Generic;

//An enum to handle all the possible scoring events
public enum ScoreEvent {
	draw,
	mine,
	mineGold,
	gameWin,
	gameLoss
}

public class Prospector : MonoBehaviour {
	static public Prospector 	S;
	static public int			SCORE_FROM_PREV_ROUND = 0;
	static public int			HIGH_SCORE = 0;

	public Deck					deck;
	public TextAsset			deckXML;

	public Layout				layout;
	public TextAsset			layoutXML;
	public Vector3				layoutCenter;
	public float				xOffset = 3;
	public float				yOffset = -2.5f;
	public Transform			layoutAnchor;

	public CardProspector		target;
	public List<CardProspector>	tableau;
	public List<CardProspector>	discardPile;

	void Awake(){
		S = this;
		//Check for a high score in PlayerPrefs
		if (PlayerPrefs.HasKey ("ProspectorHighScore")) {
			HIGH_SCORE = PlayerPrefs.GetInt ("ProspectorHighScore");
		}
		//Add the score from last round, which will be >0 if it was a win
		score += SCORE_FROM_PREV_ROUND;
		//and reset
		SCORE_FROM_PREV_ROUND = 0;
	}

	public List<CardProspector>		drawPile;

	//Fields to track score info
	public int						chain = 0;
	public int						scoreRun = 0;
	public int						score = 0;

	void Start() {
		deck = GetComponent<Deck> ();
		deck.InitDeck (deckXML.text);
		Deck.Shuffle (ref deck.cards);

		layout = GetComponent<Layout> ();	//get the layout
		layout.ReadLayout (layoutXML.text);	//pass LayoutXML to it

		drawPile = ConvertListCardsToListCardProspectors (deck.cards);
		LayoutGame ();
	}

	//The Draw function will pull a single card from the drawpile and return it
	CardProspector Draw () {
		CardProspector cd = drawPile [0];	//pull the oth cardprospector
		drawPile.RemoveAt (0);				//then remove it from List<> drawPile
		return(cd);							//and return it
	}

	//Convert from the layoutID int to the cardprospector with that id
	CardProspector FindCardByLayoutID(int layoutID) {
		foreach (CardProspector tCP in tableau) {
			//search through all cards in the tableau list<>
			if (tCP.layoutID == layoutID) {
				//if the card has the same id, return it
				return(tCP);
			}
		}
		//if its not found, return null
		return(null);
	}

	void LayoutGame() {
		//create an empty GameObject to serve as an anchor for the tableau
		if (layoutAnchor == null) {
			GameObject tGO = new GameObject ("_LayoutAnchor");
			// ^^ create an empty gameobject named _LayoutAnchor in the hierarchy
			layoutAnchor = tGO.transform;			//grab its transform
			layoutAnchor.transform.position = layoutCenter;
		}

		CardProspector cp;
		//Follow the layout
		foreach (SlotDef tSD in layout.slotDefs) {
			// itherate through all the slotdefs in the layou.slotdefs as tSD
			cp = Draw ();
			cp.faceUP = tSD.faceUp;
			cp.transform.parent = layoutAnchor;
			cp.transform.localPosition = new Vector3 (
				layout.multiplier.x * tSD.x,
				layout.multiplier.y * tSD.y,
				-tSD.layerID);
			cp.layoutID = tSD.id;
			cp.slotDef = tSD;
			cp.state = CardState.tableau;

			cp.SetSortingLayerName(tSD.layerName); //set sorting layers

			tableau.Add (cp);
		}

		//Set which cards are hiding others
		foreach (CardProspector tCP in tableau) {
			foreach (int hid in tCP.slotDef.hiddenBy) {
				cp = FindCardByLayoutID (hid);
				tCP.hiddenBy.Add (cp);
			}
		}

		//Set up the initial target card
		MoveToTarget (Draw ());

		//Set up the Draw Pile
		UpdateDrawPile ();
	}

	List<CardProspector> ConvertListCardsToListCardProspectors(List<Card> lCD) {
		List<CardProspector> lCP = new List<CardProspector> ();
		CardProspector tCP;
		foreach (Card tCD in lCD) {
			tCP = tCD as CardProspector;
			lCP.Add (tCP);
		}
		return(lCP);
	}

	//CardClicked is called any time a card in the game is clicked
	public void CardClicked(CardProspector cd) {
		//The reaction is determined by the state of the clicked card
		switch (cd.state) {
		case CardState.target:
			//clicking the target card does nothing
			break;
		case CardState.drawpile:
			//Clicking any card in the drawpile will draw the next card
			MoveToDiscard (target);
			MoveToTarget (Draw ());
			UpdateDrawPile ();
			ScoreManager(ScoreEvent.draw);
			break;
		case CardState.tableau:
			//Clicking a card in the tableau will check if its a valid play
			bool validMatch = true;
			if (!cd.faceUP){
				validMatch = false;
			}
			if (!AdjacentRank(cd, target)) {
				validMatch = false;
			}
			if (!validMatch) return;
			tableau.Remove (cd);
			MoveToTarget(cd);
			SetTableauFaces(); //update tablau card faceups
			ScoreManager(ScoreEvent.mine);
			break;
		}
		//Check to see whether the game is over or not
		CheckForGameOver ();
	}

	//Moves the current target to the discardPile
	void MoveToDiscard(CardProspector cd) {
		//Set the state of the card to discard
		cd.state = CardState.discard;
		discardPile.Add (cd);
		cd.transform.parent = layoutAnchor;
		cd.transform.localPosition = new Vector3 (
			layout.multiplier.x * layout.discardPile.x,
			layout.multiplier.y * layout.discardPile.y,
			-layout.discardPile.layerID + 0.5f);
		//Position it on the discardPile
		cd.faceUP = true;
		cd.SetSortingLayerName (layout.discardPile.layerName);
		cd.SetSortOrder (-100 + discardPile.Count);
	}

	//Make cd the new target card
	void MoveToTarget(CardProspector cd) {
		//If there is currently a target card, move it to discard pile
		if (target != null)
			MoveToDiscard (target);
		target = cd;
		cd.state = CardState.target;
		cd.transform.parent = layoutAnchor;
		//Move to the target position
		cd.transform.localPosition = new Vector3 (
			layout.multiplier.x * layout.discardPile.x,
			layout.multiplier.y * layout.discardPile.y,
			-layout.discardPile.layerID);
		cd.faceUP = true;
		cd.SetSortingLayerName (layout.discardPile.layerName);
		cd.SetSortOrder (0);
	}

	//Arranges all the cards of the drawpile to show how many are left
	void UpdateDrawPile() {
		CardProspector cd;
		//Go through all the cards of the draw pile
		for (int i=0; i<drawPile.Count; i++) {
			cd = drawPile [i];
			cd.transform.parent = layoutAnchor;
			Vector2 dpStagger = layout.drawPile.stagger;
			cd.transform.localPosition = new Vector3 (
				layout.multiplier.x * (layout.drawPile.x + i * dpStagger.x),
				layout.multiplier.y * (layout.drawPile.y + i * dpStagger.y),
				-layout.drawPile.layerID + 0.1f * i);
			cd.faceUP = false;
			cd.state = CardState.drawpile;
			cd.SetSortingLayerName (layout.drawPile.layerName);
			cd.SetSortOrder (-10 * i);
		}
	}

	//Return true if the two cards are adjacent in rank ( A + K wraparound)
	public bool AdjacentRank(CardProspector c0, CardProspector c1) {
		//If either card is face-down, its not adjacent
		if (!c0.faceUP || !c1.faceUP)
			return(false);

		//If they are 1 apart, they are adjacent
		if (Mathf.Abs (c0.rank - c1.rank) == 1) {
			return(true);
		}
		//If one is A and the other King, theyre adjacent
		if (c0.rank == 1 && c1.rank == 13)
			return(true);
		if (c0.rank == 13 && c1.rank == 1)
			return(true);
		return(false);
	}

	//This turns cards in the mine faceup or face down
	void SetTableauFaces() {
		foreach (CardProspector cd in tableau) {
			bool fup = true;
			foreach (CardProspector cover in cd.hiddenBy) {
				//if either of the covering cards are in the tableau
				if (cover.state == CardState.tableau) {
					fup = false; //then this card is facedown
				}
			}
			cd.faceUP = fup;
		}
	}

	//Test whether the game is over
	void CheckForGameOver() {
		//If the tableau is empty, the game is over
		if (tableau.Count == 0) {
			//Call Gameover() with a win
			GameOver (true);
			return;
		}
		//if there are still cards in the draw pile, the games not over
		if (drawPile.Count > 0) {
			return;
		}
		//Check for remaining valid plays
		foreach (CardProspector cd in tableau) {
			if (AdjacentRank (cd, target)) {
				//if there is a valid play, the games not over
				return;
			}
		}
		//Since there are no valid plays, the game is over
		//call GamOver with a loss
		GameOver (false);
	}

	//Called when the game is over. Simple for now, but expandable
	void GameOver(bool won) {
		if (won) {
			ScoreManager(ScoreEvent.gameWin);
		} else {
			ScoreManager(ScoreEvent.gameLoss);
		}
		//Reload the scene, resetting the game
		Application.LoadLevel ("__Prospector_Scene_0");
	}

	//ScoreManager handles all of the scoring
	void ScoreManager(ScoreEvent sEvt) {
		switch (sEvt) {
		case ScoreEvent.draw:
		case ScoreEvent.gameWin:
		case ScoreEvent.gameLoss:
			chain = 0;
			score += scoreRun;
			scoreRun = 0;
			break;
		case ScoreEvent.mine:
			chain++;
			scoreRun += chain;
			break;
		}

		//This second switch statement handles round wins and losses
		switch (sEvt) {
		case ScoreEvent.gameWin:
			Prospector.SCORE_FROM_PREV_ROUND = score;
			print ("You won this round! Round score: " + score);
			break;
		case ScoreEvent.gameLoss:
			if (Prospector.HIGH_SCORE <= score) {
				print ("You got the high score! High score: " + score);
				Prospector.HIGH_SCORE = score;
				PlayerPrefs.SetInt ("ProspectorHighScore", score);
			} else {
				print ("Your final score for the game was: " + score);
			}
			break;
		default:
			print ("score: " + score + "  scoreRun: " + scoreRun + "  chain: " + chain);
			break;
		}
	}
}
